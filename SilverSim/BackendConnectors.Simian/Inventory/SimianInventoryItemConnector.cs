// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Simian.Common;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.StructuredData.JSON;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Simian.Inventory
{
    class SimianInventoryItemConnector : InventoryItemServiceInterface
    {
        private string m_InventoryURI;
        public int TimeoutMs = 20000;
        private GroupsServiceInterface m_GroupsService;
        private string m_InventoryCapability;

        #region Constructor
        public SimianInventoryItemConnector(string uri, GroupsServiceInterface groupsService, string simCapability)
        {
            m_InventoryCapability = simCapability;
            m_GroupsService = groupsService;
            m_InventoryURI = uri;
        }
        #endregion

        #region Accessors
        public override InventoryItem this[UUID key]
        {
            get 
            { 
                throw new NotImplementedException(); 
            }
        }
        public override InventoryItem this[UUID PrincipalID, UUID key]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["RequestMethod"] = "GetInventoryNode";
                post["ItemID"] = (string)key;
                post["OwnerID"] = (string)PrincipalID;
                post["IncludeFolders"] = "1";
                post["IncludeItems"] = "1";
                post["ChildrenOnly"] = "1";

                Map res = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
                if (res["Success"].AsBoolean && res.ContainsKey("Items") && res["Items"] is AnArray)
                {
                    foreach (IValue iv in (AnArray)res["Items"])
                    {
                        if(iv is Map)
                        {
                            Map m = (Map)iv;
                            if(m["Type"].ToString() == "Item")
                            {
                                return SimianInventoryConnector.ItemFromMap(m, m_GroupsService);
                            }
                        }
                    }
                }
                throw new InventoryItemNotFound(key);
            }
        }
        #endregion

        public override void Add(InventoryItem item)
        {
            Map perms = new Map();
            perms.Add("BaseMask", (uint)item.Permissions.Base);
            perms.Add("EveryoneMask", (uint)item.Permissions.EveryOne);
            perms.Add("GroupMask", (uint)item.Permissions.Group);
            perms.Add("NextOwnerMask", (uint)item.Permissions.NextOwner);
            perms.Add("OwnerMask", (uint)item.Permissions.Current);

            Map extraData = new Map();
            extraData.Add("Flags", item.Flags);
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
            post["ExtraData"] = JSON.Serialize(extraData);

            Map m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                throw new InventoryItemNotStored(item.ID);
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
                    if (item.Flags == 1)
                    {
                        gestures.Add(item.ID);
                    }

                    bool updateNeeded = false;
                    foreach (IValue v in (AnArray)m["Gestures"])
                    {
                        if(v.AsUUID == item.ID && item.Flags != 1)
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
                    post["Gestures"] = JSON.Serialize(json_gestures);
                    m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
                }
                catch
                {

                }
            }
        }

        public override void Update(InventoryItem item)
        {
            Add(item);
        }

        public override void Delete(UUID PrincipalID, UUID ID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "RemoveInventoryNode";
            post["OwnerID"] = (string)PrincipalID;
            post["ItemID"] = (string)ID;
            Map m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryItemNotFound(ID);
            }
        }

        public override void Move(UUID PrincipalID, UUID ID, UUID newFolder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "MoveInventoryNodes";
            post["OwnerID"] = (string)PrincipalID;
            post["FolderID"] = (string)newFolder;
            post["Items"] = (string)ID;
            Map m = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryItemNotStored(ID);
            }
        }
    }
}
