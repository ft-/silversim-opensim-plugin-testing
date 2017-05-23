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

using SilverSim.BackendConnectors.Simian.Common;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using SilverSim.Types.StructuredData.Json;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Simian.Inventory
{
    public partial class SimianInventoryConnector : IInventoryItemServiceInterface
    {
        #region Accessors
        bool IInventoryItemServiceInterface.TryGetValue(UUID key, out InventoryItem item)
        {
            throw new NotSupportedException();
        }

        bool IInventoryItemServiceInterface.ContainsKey(UUID key)
        {
            throw new NotSupportedException();
        }

        InventoryItem IInventoryItemServiceInterface.this[UUID key]
        {
            get { throw new NotSupportedException(); }
        }

        bool IInventoryItemServiceInterface.TryGetValue(UUID principalID, UUID key, out InventoryItem item)
        {
            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "GetInventoryNode",
                ["ItemID"] = (string)key,
                ["OwnerID"] = (string)principalID,
                ["IncludeFolders"] = "1",
                ["IncludeItems"] = "1",
                ["ChildrenOnly"] = "1"
            };
            Map res = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items"))
            {
                var resarray = res["Items"] as AnArray;
                if (resarray != null)
                {
                    foreach (IValue iv in resarray)
                    {
                        var m = iv as Map;
                        if (m != null && m["Type"].ToString() == "Item")
                        {
                            item = ItemFromMap(m, m_GroupsService);
                            return true;
                        }
                    }
                }
            }

            item = default(InventoryItem);
            return false;
        }

        bool IInventoryItemServiceInterface.ContainsKey(UUID principalID, UUID key)
        {
            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "GetInventoryNode",
                ["ItemID"] = (string)key,
                ["OwnerID"] = (string)principalID,
                ["IncludeFolders"] = "1",
                ["IncludeItems"] = "1",
                ["ChildrenOnly"] = "1"
            };
            Map res = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items"))
            {
                var resarray = res["Items"] as AnArray;
                if (resarray != null)
                {
                    foreach (IValue iv in resarray)
                    {
                        var m = iv as Map;
                        if (m != null && m["Type"].ToString() == "Item")
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        InventoryItem IInventoryItemServiceInterface.this[UUID principalID, UUID key]
        {
            get
            {
                InventoryItem item;
                if (!Item.TryGetValue(principalID, key, out item))
                {
                    throw new InventoryItemNotFoundException(key);
                }
                return item;
            }
        }

        List<InventoryItem> IInventoryItemServiceInterface.this[UUID principalID, List<UUID> keys]
        {
            get
            {
                var res = new List<InventoryItem>();
                foreach(UUID key in keys)
                {
                    try
                    {
                        res.Add(Item[principalID, key]);
                    }
                    catch
                    {
                        /* nothing to do here */
                    }
                }

                return res;
            }
        }
        #endregion

        void IInventoryItemServiceInterface.Add(InventoryItem item)
        {
            var perms = new Map
            {
                { "BaseMask", (uint)item.Permissions.Base },
                { "EveryoneMask", (uint)item.Permissions.EveryOne },
                { "GroupMask", (uint)item.Permissions.Group },
                { "NextOwnerMask", (uint)item.Permissions.NextOwner },
                { "OwnerMask", (uint)item.Permissions.Current }
            };
            var extraData = new Map
            {
                { "Flags", (uint)item.Flags },
                { "GroupID", item.Group.ID },
                { "GroupOwned", item.IsGroupOwned },
                { "SalePrice", item.SaleInfo.Price },
                { "SaleType", (int)item.SaleInfo.Type },
                { "Permissions", perms }
            };
            string invContentType = SimianInventoryConnector.ContentTypeFromInventoryType(item.InventoryType);
            string assetContentType = SimianInventoryConnector.ContentTypeFromAssetType(item.AssetType);
            if (invContentType != assetContentType)
            {
                extraData.Add("LinkedItemType", assetContentType);
            }

            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "AddInventoryItem",
                ["ItemID"] = (string)item.ID,
                ["AssetID"] = (string)item.AssetID,
                ["ParentID"] = (string)item.ParentFolderID,
                ["OwnerID"] = (string)item.Owner,
                ["Name"] = item.Name,
                ["Description"] = item.Description,
                ["CreatorID"] = (string)item.Creator.ID,
                ["CreatorData"] = item.Creator.CreatorData,
                ["ContentType"] = invContentType,
                ["ExtraData"] = Json.Serialize(extraData)
            };
            Map m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                throw new InventoryItemNotStoredException(item.ID);
            }

            if(item.AssetType == AssetType.Gesture)
            {
                try
                {
                    post.Clear();
                    post["RequestMethod"] = "GetUser";
                    post["UserID"] = (string)item.Owner.ID;
                    m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
                    if (!m["Success"].AsBoolean || !m.ContainsKey("Gestures") || !(m["Gestures"] is AnArray))
                    {
                        return;
                    }
                    var gestures = new List<UUID>();
                    if ((uint)item.Flags == 1)
                    {
                        gestures.Add(item.ID);
                    }

                    bool updateNeeded = false;
                    foreach (IValue v in (AnArray)m["Gestures"])
                    {
                        if(v.AsUUID == item.ID && (uint)item.Flags != 1)
                        {
                            updateNeeded = true;
                        }
                        else if (!gestures.Contains(v.AsUUID))
                        {
                            gestures.Add(v.AsUUID);
                            if(v.AsUUID == item.ID)
                            {
                                updateNeeded = true;
                            }
                        }
                        else if(v.AsUUID == item.ID)
                        {
                            /* no update needed */
                            return;
                        }
                    }
                    if(!updateNeeded)
                    {
                        return;
                    }
                    var json_gestures = new AnArray();
                    foreach(UUID u in gestures)
                    {
                        json_gestures.Add(u);
                    }

                    post.Clear();
                    post["RequestMethod"] = "AddUserData";
                    post["UserID"] = (string)item.Owner.ID;
                    post["Gestures"] = Json.Serialize(json_gestures);
                    SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
                }
                catch
                {
                    /* no action needed */
                }
            }
        }

        void IInventoryItemServiceInterface.Update(InventoryItem item)
        {
            Item.Add(item);
        }

        void IInventoryItemServiceInterface.Delete(UUID principalID, UUID id)
        {
            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "RemoveInventoryNode",
                ["OwnerID"] = (string)principalID,
                ["ItemID"] = (string)id
            };
            Map m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryItemNotFoundException(id);
            }
        }

        void IInventoryItemServiceInterface.Move(UUID principalID, UUID id, UUID newFolder)
        {
            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "MoveInventoryNodes",
                ["OwnerID"] = (string)principalID,
                ["FolderID"] = (string)newFolder,
                ["Items"] = (string)id
            };
            Map m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryItemNotStoredException(id);
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
