// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    #region Service Implementation
    public class RobustInventoryConnector : InventoryServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST INVENTORY");

        private string m_InventoryURI;
        private RobustInventoryFolderConnector m_FolderService;
        private RobustInventoryItemConnector m_ItemService;
        private GroupsServiceInterface m_GroupsService;
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

        public override void checkInventory(UUID PrincipalID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)PrincipalID;
            post["METHOD"] = "CREATEUSERINVENTORY";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryInaccessible();
            }
        }

        public override List<InventoryItem> getActiveGestures(UUID PrincipalID)
        {
            Dictionary<string, string> post = new Dictionary<string,string>();
            post["PRINCIPAL"] = (string)PrincipalID;
            post["METHOD"] = "GETACTIVEGESTURES";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!(map["ITEMS"] is Map))
            {
                throw new InventoryInaccessible();
            }

            List<InventoryItem> items = new List<InventoryItem>();
            foreach(KeyValuePair<string, IValue> i in (Map)map["ITEMS"])
            {
                if(i.Value is Map)
                {
                    items.Add(ItemFromMap((Map)i.Value, m_GroupsService));
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
            if (map["CreatorData"].AsString.ToString() == "")
            {
                item.Creator.ID = map["CreatorId"].AsUUID;
            }
            else
            {
                item.Creator = new UUI(map["CreatorId"].AsUUID, map["CreatorData"].AsString.ToString());
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
            item.IsGroupOwned = map["GroupOwned"].AsBoolean;
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
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new RobustInventoryConnector(ownSection.GetString("URI"));
        }
    }
    #endregion

}
