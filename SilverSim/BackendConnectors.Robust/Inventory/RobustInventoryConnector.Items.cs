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

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Inventory;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    public partial class RobustInventoryConnector : IInventoryItemServiceInterface
    {
        private bool m_isMultipleSupported = true;

        #region Accessors
        bool IInventoryItemServiceInterface.TryGetValue(UUID key, out InventoryItem item)
        {
            var post = new Dictionary<string, string>
            {
                ["ID"] = (string)key,
                ["METHOD"] = "GETITEM"
            };
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

            var itemmap = map["item"] as Map;
            if (itemmap == null)
            {
                item = default(InventoryItem);
                return false;
            }

            item = ItemFromMap(itemmap, m_GroupsService);
            return true;
        }

        bool IInventoryItemServiceInterface.ContainsKey(UUID key)
        {
            var post = new Dictionary<string, string>
            {
                ["ID"] = (string)key,
                ["METHOD"] = "GETITEM"
            };
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("item"))
            {
                return false;
            }
            var itemmap = map["item"] as Map;
            if (itemmap == null)
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
                    throw new InventoryItemNotFoundException(key);
                }
                return item;
            }
        }

        bool IInventoryItemServiceInterface.TryGetValue(UUID principalID, UUID key, out InventoryItem item)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["ID"] = (string)key,
                ["METHOD"] = "GETITEM"
            };
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

            var itemmap = map["item"] as Map;
            if (itemmap == null)
            {
                item = default(InventoryItem);
                return false;
            }

            item = ItemFromMap(itemmap, m_GroupsService);
            return true;
        }

        bool IInventoryItemServiceInterface.ContainsKey(UUID principalID, UUID key)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["ID"] = (string)key,
                ["METHOD"] = "GETITEM"
            };
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }

            if (!map.ContainsKey("item"))
            {
                return false;
            }

            var itemmap = map["item"] as Map;
            return itemmap != null;
        }

        InventoryItem IInventoryItemServiceInterface.this[UUID principalID, UUID key]
        {
            get
            {
                var post = new Dictionary<string, string>
                {
                    ["PRINCIPAL"] = (string)principalID,
                    ["ID"] = (string)key,
                    ["METHOD"] = "GETITEM"
                };
                Map map;
                using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }
                Map itemmap;
                if(!map.TryGetValue("item", out itemmap))
                {
                    throw new InventoryItemNotFoundException(key);
                }

                return ItemFromMap(itemmap, m_GroupsService);
            }
        }

        private List<InventoryItem> GetItemsBySingleRequests(UUID principalID, List<UUID> itemids)
        {
            var res = new List<InventoryItem>();
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

                var post = new Dictionary<string, string>
                {
                    ["PRINCIPAL"] = (string)principalID,
                    ["ITEMS"] = string.Join(",", itemids),
                    ["COUNT"] = itemids.Count.ToString(), /* <- some redundancy here for whatever unknown reason, it could have been derived from ITEMS anyways */
                    ["METHOD"] = "GETMULTIPLEITEMS"
                };
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

                var items = new List<InventoryItem>();
                bool anyResponse = false;
                foreach(KeyValuePair<string, IValue> kvp in map)
                {
                    if(kvp.Key.StartsWith("item_"))
                    {
                        anyResponse = true;
                        var itemmap = kvp.Value as Map;
                        if(itemmap != null)
                        {
                            items.Add(ItemFromMap(itemmap, m_GroupsService));
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

        private Dictionary<string, string> SerializeItem(InventoryItem item) => new Dictionary<string, string>
        {
            ["ID"] = (string)item.ID,
            ["AssetID"] = (string)item.AssetID,
            ["CreatorId"] = (string)item.Creator.ID,
            ["GroupID"] = (string)item.Group.ID,
            ["GroupOwned"] = item.IsGroupOwned.ToString(),
            ["Folder"] = (string)item.ParentFolderID,
            ["Owner"] = (string)item.Owner.ID,
            ["LastOwner"] = (string)item.LastOwner.ID,
            ["Name"] = item.Name,
            ["InvType"] = ((int)item.InventoryType).ToString(),
            ["AssetType"] = ((int)item.AssetType).ToString(),
            ["BasePermissions"] = ((uint)item.Permissions.Base).ToString(),
            ["CreationDate"] = ((uint)item.CreationDate.DateTimeToUnixTime()).ToString(),
            ["CreatorData"] = item.Creator.CreatorData,
            ["CurrentPermissions"] = ((uint)item.Permissions.Current).ToString(),
            ["GroupPermissions"] = ((uint)item.Permissions.Group).ToString(),
            ["Description"] = item.Description,
            ["EveryOnePermissions"] = ((uint)item.Permissions.EveryOne).ToString(),
            ["Flags"] = ((uint)item.Flags).ToString(),
            ["NextPermissions"] = ((uint)item.Permissions.NextOwner).ToString(),
            ["SalePrice"] = ((uint)item.SaleInfo.Price).ToString(),
            ["SaleType"] = ((uint)item.SaleInfo.Type).ToString()
        };

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
                IValue iv;
                if (map.TryGetValue("FAULT", out iv))
                {
                    switch (iv.ToString())
                    {
                        case "ParentFolder":
                            throw new InvalidParentFolderIdException();
                    }
                }
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
            var post = new Dictionary<string, string>
            {
                ["ITEMS[]"] = (string)id,
                ["PRINCIPAL"] = (string)principalID,
                ["METHOD"] = "DELETEITEMS"
            };
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
            var post = new Dictionary<string, string>
            {
                ["IDLIST[]"] = (string)id,
                ["DESTLIST[]"] = (string)newFolder,
                ["PRINCIPAL"] = (string)principalID,
                ["METHOD"] = "MOVEITEMS"
            };
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                IValue iv;
                if(map.TryGetValue("FAULT", out iv))
                {
                    switch(iv.ToString())
                    {
                        case "ParentFolder":
                            throw new InvalidParentFolderIdException();
                    }
                }
                throw new InventoryItemNotFoundException(id);
            }
        }

        List<UUID> IInventoryItemServiceInterface.Delete(UUID principalID, List<UUID> itemids)
        {
            var deleted = new List<UUID>();
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
