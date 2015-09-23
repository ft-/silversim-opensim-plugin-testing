// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Inventory;
using System.Collections.Generic;
using System.Net;
using System.Web;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    class RobustInventoryItemConnector : InventoryItemServiceInterface
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
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
                if (!(map["item"] is Map))
                {
                    throw new InventoryInaccessible();
                }

                return RobustInventoryConnector.ItemFromMap((Map)map["item"], m_GroupsService);
            }
        }

        public override InventoryItem this[UUID PrincipalID, UUID key]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)PrincipalID;
                post["ID"] = (string)key;
                post["METHOD"] = "GETITEM";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
                if (!(map["item"] is Map))
                {
                    throw new InventoryInaccessible();
                }

                return RobustInventoryConnector.ItemFromMap((Map)map["item"], m_GroupsService);
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
                    map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
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
                        if(kvp.Value is Map)
                        {
                            items.Add(RobustInventoryConnector.ItemFromMap((Map)kvp.Value, m_GroupsService));
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
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if(!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotStored(item.ID);
            }
        }

        public override void Update(InventoryItem item)
        {
            Dictionary<string, string> post = SerializeItem(item);
            post["METHOD"] = "UPDATEITEM";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotStored(item.ID);
            }
        }

        public override void Delete(UUID PrincipalID, UUID ID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ITEMS[]"] = (string)ID;
            post["PRINCIPAL"] = (string)PrincipalID;
            post["METHOD"] = "DELETEITEMS";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotFound(ID);
            }
        }

        public override void Move(UUID PrincipalID, UUID ID, UUID newFolder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["IDLIST[]"] = (string)ID;
            post["DESTLIST[]"] = (string)newFolder;
            post["PRINCIPAL"] = (string)PrincipalID;
            post["METHOD"] = "MOVEITEMS";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotFound(ID);
            }
        }
    }
}
