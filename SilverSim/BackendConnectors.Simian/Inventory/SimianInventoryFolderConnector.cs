// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Simian.Common;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Simian.Inventory
{
    public sealed class SimianInventoryFolderConnector : InventoryFolderServiceInterface
    {
        private string m_InventoryURI;
        public int TimeoutMs = 20000;
        private GroupsServiceInterface m_GroupsService;
        private string m_SimCapability;

        #region Constructor
        public SimianInventoryFolderConnector(string uri, GroupsServiceInterface groupsService, string simCapability)
        {
            m_SimCapability = simCapability;
            m_GroupsService = groupsService;
            m_InventoryURI = uri;
        }
        #endregion

        #region Accessors
        public override InventoryFolder this[UUID principalID, UUID key]
        {
            get
            {
                List<InventoryFolder> folders = GetFolders(principalID, key);
                foreach(InventoryFolder folder in folders)
                {
                    if(folder.ID.Equals(key))
                    {
                        return folder;
                    }
                }
                throw new InventoryInaccessibleException();
            }
        }

        public override InventoryFolder this[UUID key]
        {
            get 
            { 
                throw new NotImplementedException();
            }
        }

        public override InventoryFolder this[UUID principalID, AssetType type]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                if (type == AssetType.RootFolder)
                {
                    post["RequestMethod"] = "GetInventoryNode";
                    post["ItemID"] = (string)principalID;
                    post["OwnerID"] = (string)principalID;
                    post["IncludeFolders"] = "1";
                    post["IncludeItems"] = "0";
                    post["ChildrenOnly"] = "1";
                }
                else
                {
                    post["RequestMethod"] = "GetFolderForType";
                    post["OwnerID"] = (string)principalID;
                    post["ContentType"] = SimianInventoryConnector.ContentTypeFromAssetType(type);
                }
                Map res = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
                if (res["Success"].AsBoolean && res.ContainsKey("Items"))
                {
                    AnArray resarray = res["Items"] as AnArray;
                    if(null != resarray && resarray.Count != 0)
                    {
                        Map m = resarray[0] as Map;
                        if (m != null)
                        {
                            return SimianInventoryConnector.FolderFromMap(m);
                        }
                    }
                }
                throw new InventoryInaccessibleException();
            }
        }

        public override List<InventoryFolder> GetFolders(UUID principalID, UUID key)
        {
            List<InventoryFolder> folders = new List<InventoryFolder>();
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "GetInventoryNode";
            post["ItemID"] = (string)key;
            post["OwnerID"] = (string)principalID;
            post["IncludeFolders"] = "1";
            post["IncludeItems"] = "0";
            post["ChildrenOnly"] = "1";

            Map res = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items"))
            {
                AnArray resarray = res["Items"] as AnArray;
                if(null != resarray)
                {
                    foreach (IValue iv in resarray)
                    {
                        Map m = iv as Map;
                        if (m != null && m["Type"].ToString() == "Folder")
                        {
                            folders.Add(SimianInventoryConnector.FolderFromMap(m));
                        }
                    }
                    return folders;
                }
            }
            throw new InventoryInaccessibleException();
        }

        public override List<InventoryItem> GetItems(UUID principalID, UUID key)
        {
            List<InventoryItem> items = new List<InventoryItem>();
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "GetInventoryNode";
            post["ItemID"] = (string)key;
            post["OwnerID"] = (string)principalID;
            post["IncludeFolders"] = "0";
            post["IncludeItems"] = "1";
            post["ChildrenOnly"] = "1";

            Map res = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items"))
            {
                AnArray resarray = res["Items"] as AnArray;
                if(null != resarray)
                {
                    foreach (IValue iv in resarray)
                    {
                        Map m = iv as Map;
                        if (null != m && m["Type"].ToString() == "Item")
                        {
                            items.Add(SimianInventoryConnector.ItemFromMap(m, m_GroupsService));
                        }
                    }
                    return items;
                }
            }
            throw new InventoryInaccessibleException();
        }

        #endregion

        #region Methods

        public override void Add(InventoryFolder folder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "AddInventoryFolder";
            post["FolderID"] = (string)folder.ID;
            post["ParentID"] = (string)folder.ParentFolderID;
            post["ContentType"] = ((int)folder.InventoryType).ToString();
            post["Name"] = folder.Name;
            post["OwnerID"] = (string)folder.Owner.ID;

            Map m = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                throw new InventoryFolderNotStoredException(folder.ID);
            }
        }
        public override void Update(InventoryFolder folder)
        {
            Add(folder);
        }

        public override void IncrementVersion(UUID principalID, UUID folderID)
        {
            InventoryFolder folder = this[principalID, folderID];
#warning TODO: check whether Simian has a IncrementVersion check
            folder.Version += 1;
            Update(folder);
        }

        public override void Move(UUID principalID, UUID folderID, UUID toFolderID)
        {
            InventoryFolder folder = this[principalID, folderID];
            folder.ParentFolderID = toFolderID;
            Add(folder);
        }

        public override void Delete(UUID principalID, UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "RemoveInventoryNode";
            post["OwnerID"] = (string)principalID;
            post["ItemID"] = (string)folderID;

            Map m = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }

        public override void Purge(UUID folderID)
        {
            throw new NotImplementedException();
        }

        public override void Purge(UUID principalID, UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "PurgeInventoryFolder";
            post["OwnerID"] = (string)principalID;
            post["FolderID"] = (string)folderID;

            Map m = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }
        #endregion
    }
}
