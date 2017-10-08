// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3 with
// the following clarification and special exception.

// Linking this library statically or dynamically with other modules is
// making a combined work based on this library. Thus, the terms and
// conditions of the GNU Affero General Public License cover the whole
// combination.

// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module. An independent module is a module which is not derived from
// or based on this library. If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so. If you do not wish to do so, delete this
// exception statement from your version.

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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    #region Service Implementation
    [Description("Robust Inventory Connector")]
    [PluginName("Inventory")]
    public sealed partial class RobustInventoryConnector : InventoryServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST INVENTORY CONNECTOR");

        private readonly string m_InventoryURI;
        private readonly GroupsServiceInterface m_GroupsService;

        #region Constructor
        public RobustInventoryConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            TimeoutMs = 20000;
            string uri = ownSection.GetString("URI");
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "xinventory";
            m_InventoryURI = uri;
        }

        public RobustInventoryConnector(string uri)
        {
            TimeoutMs = 20000;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "xinventory";
            m_InventoryURI = uri;
        }

        public RobustInventoryConnector(string uri, GroupsServiceInterface groupsService)
        {
            TimeoutMs = 20000;
            m_GroupsService = groupsService;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "xinventory";
            m_InventoryURI = uri;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        #region Accessors
        public int TimeoutMs { get; set; }

        public override IInventoryFolderServiceInterface Folder => this;

        public override IInventoryItemServiceInterface Item => this;

        public override void Remove(UUID scopeID, UUID accountID)
        {
            throw new NotSupportedException("Remove");
        }

        public override void CheckInventory(UUID principalID)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["METHOD"] = "CREATEUSERINVENTORY"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
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
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["METHOD"] = "GETACTIVEGESTURES"
            };
            Map map;
            using(Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            var itemmap = map["ITEMS"] as Map;
            if (itemmap == null)
            {
                throw new InventoryInaccessibleException();
            }

            var items = new List<InventoryItem>();
            foreach(KeyValuePair<string, IValue> i in itemmap)
            {
                var itemdata = i.Value as Map;
                if(itemdata != null)
                {
                    items.Add(ItemFromMap(itemdata, m_GroupsService));
                }
            }
            return items;
        }
        #endregion

        #region Map converson
        internal static InventoryFolder FolderFromMap(Map map) => new InventoryFolder()
        {
            ID = map["ID"].AsUUID,
            Owner = new UUI(map["Owner"].AsUUID),
            Name = map["Name"].AsString.ToString(),
            Version = map["Version"].AsInteger,
            DefaultType = (AssetType)map["Type"].AsInt,
            ParentFolderID = map["ParentID"].AsUUID
        };
        internal static InventoryItem ItemFromMap(Map map, GroupsServiceInterface groupsService)
        {
            var item = new InventoryItem(map["ID"].AsUUID)
            {
                AssetID = map["AssetID"].AsUUID,
                AssetType = (AssetType)map["AssetType"].AsInt,
                CreationDate = Date.UnixTimeToDateTime(map["CreationDate"].AsULong),
                Description = map["Description"].AsString.ToString(),
                Flags = (InventoryFlags)map["Flags"].AsUInt,
                ParentFolderID = map["Folder"].AsUUID,
                InventoryType = (InventoryType)map["InvType"].AsInt,
                Name = map["Name"].AsString.ToString(),
                Owner = new UUI(map["Owner"].AsUUID),
                IsGroupOwned = map["GroupOwned"].ToString().ToLowerInvariant() == "true"
            };

            string creatorData = map["CreatorData"].AsString.ToString();
            if (creatorData.Length == 0)
            {
                item.Creator.ID = map["CreatorId"].AsUUID;
            }
            else
            {
                item.Creator = new UUI(map["CreatorId"].AsUUID, creatorData);
            }

            IValue iv;
            if(map.TryGetValue("LastOwner", out iv))
            {
                item.LastOwner.ID = iv.AsUUID;
            }

            item.Permissions.Base = (InventoryPermissionsMask)map["BasePermissions"].AsUInt;
            item.Permissions.Current = (InventoryPermissionsMask)map["CurrentPermissions"].AsUInt;
            item.Permissions.EveryOne = (InventoryPermissionsMask)map["EveryOnePermissions"].AsUInt;
            item.Permissions.Group = (InventoryPermissionsMask)map["GroupPermissions"].AsUInt;
            item.Permissions.NextOwner = (InventoryPermissionsMask)map["NextPermissions"].AsUInt;

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
            item.SaleInfo.Price = map["SalePrice"].AsInt;
            item.SaleInfo.Type = (InventoryItem.SaleInfoData.SaleType) map["SaleType"].AsUInt;
            return item;
        }
        #endregion
    }
    #endregion
}
