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
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types.StructuredData.Json;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
            get 
            { 
                throw new NotSupportedException(); 
            }
        }

        bool IInventoryItemServiceInterface.TryGetValue(UUID principalID, UUID key, out InventoryItem item)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "GetInventoryNode";
            post["ItemID"] = (string)key;
            post["OwnerID"] = (string)principalID;
            post["IncludeFolders"] = "1";
            post["IncludeItems"] = "1";
            post["ChildrenOnly"] = "1";

            Map res = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items"))
            {
                AnArray resarray = res["Items"] as AnArray;
                if (null != resarray)
                {
                    foreach (IValue iv in resarray)
                    {
                        Map m = iv as Map;
                        if (null != m && m["Type"].ToString() == "Item")
                        {
                            item = SimianInventoryConnector.ItemFromMap(m, m_GroupsService);
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "GetInventoryNode";
            post["ItemID"] = (string)key;
            post["OwnerID"] = (string)principalID;
            post["IncludeFolders"] = "1";
            post["IncludeItems"] = "1";
            post["ChildrenOnly"] = "1";

            Map res = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items"))
            {
                AnArray resarray = res["Items"] as AnArray;
                if (null != resarray)
                {
                    foreach (IValue iv in resarray)
                    {
                        Map m = iv as Map;
                        if (null != m && m["Type"].ToString() == "Item")
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

        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotSwallowErrorsCatchingNonSpecificExceptionsRule")]
        List<InventoryItem> IInventoryItemServiceInterface.this[UUID principalID, List<UUID> keys]
        {
            get
            {
                List<InventoryItem> res = new List<InventoryItem>();
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
            Map perms = new Map();
            perms.Add("BaseMask", (uint)item.Permissions.Base);
            perms.Add("EveryoneMask", (uint)item.Permissions.EveryOne);
            perms.Add("GroupMask", (uint)item.Permissions.Group);
            perms.Add("NextOwnerMask", (uint)item.Permissions.NextOwner);
            perms.Add("OwnerMask", (uint)item.Permissions.Current);

            Map extraData = new Map();
            extraData.Add("Flags", (uint)item.Flags);
            extraData["GroupID"] = item.Group.ID;
            extraData.Add("GroupOwned", item.IsGroupOwned);
            extraData.Add("SalePrice", item.SaleInfo.Price);
            extraData.Add("SaleType", (int)item.SaleInfo.Type);
            extraData.Add("Permissions", perms);

            string invContentType = SimianInventoryConnector.ContentTypeFromInventoryType(item.InventoryType);
            string assetContentType = SimianInventoryConnector.ContentTypeFromAssetType(item.AssetType);
            if (invContentType != assetContentType)
            {
                extraData.Add("LinkedItemType", assetContentType);
            }

            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "AddInventoryItem";
            post["ItemID"] = (string)item.ID;
            post["AssetID"] = (string)item.AssetID;
            post["ParentID"] = (string)item.ParentFolderID;
            post["OwnerID"] = (string)item.Owner;
            post["Name"] = item.Name;
            post["Description"] = item.Description;
            post["CreatorID"] = (string)item.Creator.ID;
            post["CreatorData"] = item.Creator.CreatorData;
            post["ContentType"] = invContentType;
            post["ExtraData"] = Json.Serialize(extraData);

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
                    List<UUID> gestures = new List<UUID>();
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
                    AnArray json_gestures = new AnArray();
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "RemoveInventoryNode";
            post["OwnerID"] = (string)principalID;
            post["ItemID"] = (string)id;
            Map m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryItemNotFoundException(id);
            }
        }

        void IInventoryItemServiceInterface.Move(UUID principalID, UUID id, UUID newFolder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "MoveInventoryNodes";
            post["OwnerID"] = (string)principalID;
            post["FolderID"] = (string)newFolder;
            post["Items"] = (string)id;
            Map m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryItemNotStoredException(id);
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
