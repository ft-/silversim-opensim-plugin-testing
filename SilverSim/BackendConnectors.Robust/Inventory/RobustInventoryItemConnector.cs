// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Inventory;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Web;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
    public sealed class RobustInventoryItemConnector : InventoryItemServiceInterface
    {
        private string m_InventoryURI;
        public int TimeoutMs = 20000;
        private GroupsServiceInterface m_GroupsService;
        bool m_isMultipleSupported = true;

        #region Constructor
        public RobustInventoryItemConnector(string uri, GroupsServiceInterface groupsService)
        {
            m_GroupsService = groupsService;
            m_InventoryURI = uri;
        }
        #endregion

        #region Accessors
        public override InventoryItem this[UUID key]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["ID"] = (string)key;
                post["METHOD"] = "GETITEM";
                Map map;
                using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }
                Map itemmap = map["item"] as Map;
                if (null == itemmap)
                {
                    throw new InventoryInaccessibleException();
                }

                return RobustInventoryConnector.ItemFromMap(itemmap, m_GroupsService);
            }
        }

        public override InventoryItem this[UUID principalID, UUID key]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)principalID;
                post["ID"] = (string)key;
                post["METHOD"] = "GETITEM";
                Map map;
                using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }
                Map itemmap = map["item"] as Map;
                if (null == itemmap)
                {
                    throw new InventoryInaccessibleException();
                }

                return RobustInventoryConnector.ItemFromMap(itemmap, m_GroupsService);
            }
        }

        public override List<InventoryItem> this[UUID principalID, List<UUID> itemids]
        {
            get
            {
                if(itemids.Count == 0)
                {
                    return new List<InventoryItem>();
                }

                /* when the service failed for being not supported, we do not even try it again in that case */
                if(!m_isMultipleSupported)
                {
                    return base[principalID, itemids];
                }

                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)principalID;
                post["ITEMS"] = string.Join(",", itemids);
                post["COUNT"] = itemids.Count.ToString(); /* <- some redundancy here for whatever unknown reason, it could have been derived from ITEMS anyways */
                post["METHOD"] = "GETMULTIPLEITEMS";
                Map map;

                try
                {
                    using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
                    {
                        map = OpenSimResponse.Deserialize(s);
                    }
                }
                catch (HttpRequestHandler.BadHttpResponseException)
                {
                    m_isMultipleSupported = false;
                    return base[principalID, itemids];
                }
                catch (HttpException e)
                {
                    if (e.GetHttpCode() == (int)HttpStatusCode.BadGateway)
                    {
                        return base[principalID, itemids];
                    }
                    else
                    {
                        m_isMultipleSupported = false;
                        return base[principalID, itemids];
                    }
                }

                List<InventoryItem> items = new List<InventoryItem>();
                bool anyResponse = false;
                foreach(KeyValuePair<string, IValue> kvp in map)
                {
                    if(kvp.Key.StartsWith("item_"))
                    {
                        anyResponse = true;
                        Map itemmap = kvp.Value as Map;
                        if(null != itemmap)
                        {
                            items.Add(RobustInventoryConnector.ItemFromMap(itemmap, m_GroupsService));
                        }
                    }
                }

                /* check for fallback */
                if(!anyResponse)
                {
                    items = base[principalID, itemids];
                    if(items.Count > 0)
                    {
                        m_isMultipleSupported = false;
                    }
                }
                
                return items;
            }
        }
        #endregion

        private Dictionary<string, string> SerializeItem(InventoryItem item)
        {
            Dictionary<string, string> post = new Dictionary<string,string>();
            post["ID"] = (string)item.ID;
            post["AssetID"] = (string)item.AssetID;
            post["CreatorId"] = (string)item.Creator.ID;
            post["GroupID"] = (string)item.Group.ID;
            post["GroupOwned"] = item.IsGroupOwned.ToString();
            post["Folder"] = (string)item.ParentFolderID;
            post["Owner"] = (string)item.Owner.ID;
            post["Name"] = item.Name;
            post["InvType"] = ((int)item.InventoryType).ToString();
            post["AssetType"] = ((uint)item.AssetType).ToString();
            post["BasePermissions"] = ((uint)item.Permissions.Base).ToString();
            post["CreationDate"] = ((uint)item.CreationDate.DateTimeToUnixTime()).ToString();
            post["CreatorData"] = item.Creator.CreatorData;
            post["CurrentPermissions"] = ((uint)item.Permissions.Current).ToString();
            post["GroupPermissions"] = ((uint)item.Permissions.Group).ToString();
            post["Description"] = item.Description;
            post["EveryOnePermissions"] = ((uint)item.Permissions.EveryOne).ToString();
            post["Flags"] = item.Flags.ToString();
            post["NextPermissions"] = ((uint)item.Permissions.NextOwner).ToString();
            post["SalePrice"] = ((uint)item.SaleInfo.Price).ToString();
            post["SaleType"] = ((uint)item.SaleInfo.Type).ToString();

            return post;
        }

        public override void Add(InventoryItem item)
        {
            Dictionary<string, string> post = SerializeItem(item);
            post["METHOD"] = "ADDITEM";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if(!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotStoredException(item.ID);
            }
        }

        public override void Update(InventoryItem item)
        {
            Dictionary<string, string> post = SerializeItem(item);
            post["METHOD"] = "UPDATEITEM";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotStoredException(item.ID);
            }
        }

        public override void Delete(UUID principalID, UUID id)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ITEMS[]"] = (string)id;
            post["PRINCIPAL"] = (string)principalID;
            post["METHOD"] = "DELETEITEMS";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotFoundException(id);
            }
        }

        public override void Move(UUID principalID, UUID id, UUID newFolder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["IDLIST[]"] = (string)id;
            post["DESTLIST[]"] = (string)newFolder;
            post["PRINCIPAL"] = (string)principalID;
            post["METHOD"] = "MOVEITEMS";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotFoundException(id);
            }
        }
    }
}
