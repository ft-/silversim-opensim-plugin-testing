// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    #region Service Implementation
    [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
    [Description("Robust Inventory Connector")]
    public class RobustInventoryConnector : InventoryServiceInterface, IPlugin
    {
        readonly string m_InventoryURI;
        readonly RobustInventoryFolderConnector m_FolderService;
        readonly RobustInventoryItemConnector m_ItemService;
        readonly GroupsServiceInterface m_GroupsService;
        private int m_TimeoutMs = 20000;

        #region Constructor
        public RobustInventoryConnector(string uri)
        {
            if(!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "xinventory";
            m_InventoryURI = uri;
            m_ItemService = new RobustInventoryItemConnector(uri, null);
            m_ItemService.TimeoutMs = m_TimeoutMs;
            m_FolderService = new RobustInventoryFolderConnector(uri, null);
            m_FolderService.TimeoutMs = m_TimeoutMs;
        }

        public RobustInventoryConnector(string uri, GroupsServiceInterface groupsService)
        {
            m_GroupsService = groupsService;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "xinventory";
            m_InventoryURI = uri;
            m_ItemService = new RobustInventoryItemConnector(uri, m_GroupsService);
            m_ItemService.TimeoutMs = m_TimeoutMs;
            m_FolderService = new RobustInventoryFolderConnector(uri, m_GroupsService);
            m_FolderService.TimeoutMs = m_TimeoutMs;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        #region Accessors
        public int TimeoutMs
        {
            get
            {
                return m_TimeoutMs;
            }
            set
            {
                m_TimeoutMs = value;
                m_FolderService.TimeoutMs = value;
                m_ItemService.TimeoutMs = value;
            }
        }

        public override InventoryFolderServiceInterface Folder
        {
            get
            {
                return m_FolderService;
            }
        }

        public override InventoryItemServiceInterface Item
        {
            get
            {
                return m_ItemService;
            }
        }

        public override void CheckInventory(UUID principalID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["METHOD"] = "CREATEUSERINVENTORY";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryInaccessibleException();
            }
        }

        public override List<InventoryItem> GetActiveGestures(UUID principalID)
        {
            Dictionary<string, string> post = new Dictionary<string,string>();
            post["PRINCIPAL"] = (string)principalID;
            post["METHOD"] = "GETACTIVEGESTURES";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            Map itemmap = map["ITEMS"] as Map;
            if (null == itemmap)
            {
                throw new InventoryInaccessibleException();
            }

            List<InventoryItem> items = new List<InventoryItem>();
            foreach(KeyValuePair<string, IValue> i in itemmap)
            {
                Map itemdata = i.Value as Map;
                if(null != itemdata)
                {
                    items.Add(ItemFromMap(itemdata, m_GroupsService));
                }
            }
            return items;
        }
        #endregion

        #region Map converson
        internal static InventoryFolder FolderFromMap(Map map)
        {
            InventoryFolder folder = new InventoryFolder();
            folder.ID = map["ID"].AsUUID;
            folder.Owner.ID = map["Owner"].AsUUID;
            folder.Name = map["Name"].AsString.ToString();
            folder.Version = map["Version"].AsInteger;
            folder.InventoryType = (InventoryType)map["Type"].AsInt;
            folder.ParentFolderID = map["ParentID"].AsUUID;
            return folder;
        }
        internal static InventoryItem ItemFromMap(Map map, GroupsServiceInterface groupsService)
        {
            InventoryItem item = new InventoryItem();
            item.ID = map["ID"].AsUUID;
            item.AssetID = map["AssetID"].AsUUID;
            item.AssetType = (AssetType)map["AssetType"].AsInt;
            item.Permissions.Base = (InventoryPermissionsMask)map["BasePermissions"].AsUInt;
            item.CreationDate = Date.UnixTimeToDateTime(map["CreationDate"].AsULong);
            string creatorData = map["CreatorData"].AsString.ToString();
            if (creatorData.Length == 0)
            {
                item.Creator.ID = map["CreatorId"].AsUUID;
            }
            else
            {
                item.Creator = new UUI(map["CreatorId"].AsUUID, creatorData);
            }
            item.Permissions.Current = (InventoryPermissionsMask)map["CurrentPermissions"].AsUInt;
            item.Description = map["Description"].AsString.ToString();
            item.Permissions.EveryOne = (InventoryPermissionsMask)map["EveryOnePermissions"].AsUInt;
            item.Flags = map["Flags"].AsUInt;
            item.ParentFolderID = map["Folder"].AsUUID;
            if (groupsService != null)
            {
                try
                {
                    item.Group = groupsService.Groups[UUI.Unknown, map["GroupID"].AsUUID];
                }
                catch
                {
                    item.Group.ID = map["GroupID"].AsUUID;
                }
            }
            else
            {
                item.Group.ID = map["GroupID"].AsUUID;
            }
            item.IsGroupOwned = map["GroupOwned"].ToString().ToLower() == "true";
            item.Permissions.Group = (InventoryPermissionsMask)map["GroupPermissions"].AsUInt;
            item.InventoryType = (InventoryType) map["InvType"].AsInt;
            item.Name = map["Name"].AsString.ToString();
            item.Permissions.NextOwner = (InventoryPermissionsMask)map["NextPermissions"].AsUInt;
            item.Owner.ID = map["Owner"].AsUUID;
            item.SaleInfo.Price = map["SalePrice"].AsInt;
            item.SaleInfo.Type = (InventoryItem.SaleInfoData.SaleType) map["SaleType"].AsUInt;
            return item;
        }
        #endregion
    }
    #endregion

    #region Factory
    [PluginName("Inventory")]
    public class RobustInventoryConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST INVENTORY CONNECTOR");
        public RobustInventoryConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustInventoryConnector(ownSection.GetString("URI"));
        }
    }
    #endregion

}
