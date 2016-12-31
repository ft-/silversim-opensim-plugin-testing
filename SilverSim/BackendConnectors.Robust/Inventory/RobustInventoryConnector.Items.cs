// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
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
    public partial class RobustInventoryConnector : IInventoryItemServiceInterface
    {
        bool m_isMultipleSupported = true;

        #region Accessors
        bool IInventoryItemServiceInterface.TryGetValue(UUID key, out InventoryItem item)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ID"] = (string)key;
            post["METHOD"] = "GETITEM";
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if(!map.ContainsKey("item"))
            {
                item = default(InventoryItem);
                return false;
            }

            Map itemmap = map["item"] as Map;
            if (null == itemmap)
            {
                item = default(InventoryItem);
                return false;
            }

            item = RobustInventoryConnector.ItemFromMap(itemmap, m_GroupsService);
            return true;
        }

        bool IInventoryItemServiceInterface.ContainsKey(UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ID"] = (string)key;
            post["METHOD"] = "GETITEM";
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("item"))
            {
                return false;
            }
            Map itemmap = map["item"] as Map;
            if (null == itemmap)
            {
                return false;
            }

            return true;
        }

        InventoryItem IInventoryItemServiceInterface.this[UUID key]
        {
            get
            {
                InventoryItem item;
                if(!Item.TryGetValue(key, out item))
                {
                    throw new InventoryInaccessibleException();
                }
                return item;
            }
        }


        bool IInventoryItemServiceInterface.TryGetValue(UUID principalID, UUID key, out InventoryItem item)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["ID"] = (string)key;
            post["METHOD"] = "GETITEM";
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }

            if(!map.ContainsKey("item"))
            {
                item = default(InventoryItem);
                return false;
            }

            Map itemmap = map["item"] as Map;
            if (null == itemmap)
            {
                item = default(InventoryItem);
                return false;
            }

            item = RobustInventoryConnector.ItemFromMap(itemmap, m_GroupsService);
            return true;
        }

        bool IInventoryItemServiceInterface.ContainsKey(UUID principalID, UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["ID"] = (string)key;
            post["METHOD"] = "GETITEM";
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }

            if (!map.ContainsKey("item"))
            {
                return false;
            }

            Map itemmap = map["item"] as Map;
            return (null != itemmap);
        }

        InventoryItem IInventoryItemServiceInterface.this[UUID principalID, UUID key]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)principalID;
                post["ID"] = (string)key;
                post["METHOD"] = "GETITEM";
                Map map;
                using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
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

        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotSwallowErrorsCatchingNonSpecificExceptionsRule")]
        List<InventoryItem> GetItemsBySingleRequests(UUID principalID, List<UUID> itemids)
        {
            List<InventoryItem> res = new List<InventoryItem>();
            foreach(UUID itemid in itemids)
            {
                try
                {
                    res.Add(Item[principalID, itemid]);
                }
                catch
                {
                    /* nothing to do here */
                }
            }

            return res;
        }

        List<InventoryItem> IInventoryItemServiceInterface.this[UUID principalID, List<UUID> itemids]
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
                    return GetItemsBySingleRequests(principalID, itemids);
                }

                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)principalID;
                post["ITEMS"] = string.Join(",", itemids);
                post["COUNT"] = itemids.Count.ToString(); /* <- some redundancy here for whatever unknown reason, it could have been derived from ITEMS anyways */
                post["METHOD"] = "GETMULTIPLEITEMS";
                Map map;

                try
                {
                    using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
                    {
                        map = OpenSimResponse.Deserialize(s);
                    }
                }
                catch (HttpClient.BadHttpResponseException)
                {
                    m_isMultipleSupported = false;
                    return GetItemsBySingleRequests(principalID, itemids);
                }
                catch (HttpException e)
                {
                    if (e.GetHttpCode() == (int)HttpStatusCode.BadGateway)
                    {
                        return GetItemsBySingleRequests(principalID, itemids);
                    }
                    else
                    {
                        m_isMultipleSupported = false;
                        return GetItemsBySingleRequests(principalID, itemids);
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
                    items = GetItemsBySingleRequests(principalID, itemids);
                    if(items.Count > 0)
                    {
                        m_isMultipleSupported = false;
                    }
                }
                
                return items;
            }
        }
        #endregion

        Dictionary<string, string> SerializeItem(InventoryItem item)
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

        void IInventoryItemServiceInterface.Add(InventoryItem item)
        {
            Dictionary<string, string> post = SerializeItem(item);
            post["METHOD"] = "ADDITEM";
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if(!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotStoredException(item.ID);
            }
        }

        void IInventoryItemServiceInterface.Update(InventoryItem item)
        {
            Dictionary<string, string> post = SerializeItem(item);
            post["METHOD"] = "UPDATEITEM";
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotStoredException(item.ID);
            }
        }

        void IInventoryItemServiceInterface.Delete(UUID principalID, UUID id)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ITEMS[]"] = (string)id;
            post["PRINCIPAL"] = (string)principalID;
            post["METHOD"] = "DELETEITEMS";
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotFoundException(id);
            }
        }

        void IInventoryItemServiceInterface.Move(UUID principalID, UUID id, UUID newFolder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["IDLIST[]"] = (string)id;
            post["DESTLIST[]"] = (string)newFolder;
            post["PRINCIPAL"] = (string)principalID;
            post["METHOD"] = "MOVEITEMS";
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryItemNotFoundException(id);
            }
        }

        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotSwallowErrorsCatchingNonSpecificExceptionsRule")]
        List<UUID> IInventoryItemServiceInterface.Delete(UUID principalID, List<UUID> itemids)
        {
            List<UUID> deleted = new List<UUID>();
            foreach (UUID id in itemids)
            {
                try
                {
                    Item.Delete(principalID, id);
                    deleted.Add(id);
                }
                catch
                {
                    /* nothing else to do */
                }
            }
            return deleted;
        }
    }
}
