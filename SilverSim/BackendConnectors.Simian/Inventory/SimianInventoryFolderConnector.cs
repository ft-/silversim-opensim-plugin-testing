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
    class SimianInventoryFolderConnector : InventoryFolderServiceInterface
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
        public override InventoryFolder this[UUID PrincipalID, UUID key]
        {
            get
            {
                List<InventoryFolder> folders = getFolders(PrincipalID, key);
                foreach(InventoryFolder folder in folders)
                {
                    if(folder.ID.Equals(key))
                    {
                        return folder;
                    }
                }
                throw new InventoryInaccessible();
            }
        }

        public override InventoryFolder this[UUID key]
        {
            get 
            { 
                throw new NotImplementedException();
            }
        }

        public override InventoryFolder this[UUID PrincipalID, AssetType type]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                if (type == AssetType.RootFolder)
                {
                    post["RequestMethod"] = "GetInventoryNode";
                    post["ItemID"] = (string)PrincipalID;
                    post["OwnerID"] = (string)PrincipalID;
                    post["IncludeFolders"] = "1";
                    post["IncludeItems"] = "0";
                    post["ChildrenOnly"] = "1";
                }
                else
                {
                    post["RequestMethod"] = "GetFolderForType";
                    post["OwnerID"] = (string)PrincipalID;
                    post["ContentType"] = SimianInventoryConnector.ContentTypeFromAssetType(type);
                }
                Map res = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
                if (res["Success"].AsBoolean && res.ContainsKey("Items") && res["Items"] is AnArray)
                {
                    Map m = (Map)((AnArray)res["Items"])[0];
                    return SimianInventoryConnector.FolderFromMap(m);
                }
                throw new InventoryInaccessible();
            }
        }

        public override List<InventoryFolder> getFolders(UUID PrincipalID, UUID key)
        {
            List<InventoryFolder> folders = new List<InventoryFolder>();
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "GetInventoryNode";
            post["ItemID"] = (string)key;
            post["OwnerID"] = (string)PrincipalID;
            post["IncludeFolders"] = "1";
            post["IncludeItems"] = "0";
            post["ChildrenOnly"] = "1";

            Map res = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items") && res["Items"] is AnArray)
            {
                foreach (IValue iv in (AnArray)res["Items"])
                {
                    if (iv is Map)
                    {
                        Map m = (Map)iv;
                        if (m["Type"].ToString() == "Folder")
                        {
                            folders.Add(SimianInventoryConnector.FolderFromMap(m));
                        }
                    }
                }
                return folders;
            }
            throw new InventoryInaccessible();
        }

        public override List<InventoryItem> getItems(UUID PrincipalID, UUID key)
        {
            List<InventoryItem> items = new List<InventoryItem>();
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "GetInventoryNode";
            post["ItemID"] = (string)key;
            post["OwnerID"] = (string)PrincipalID;
            post["IncludeFolders"] = "0";
            post["IncludeItems"] = "1";
            post["ChildrenOnly"] = "1";

            Map res = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items") && res["Items"] is AnArray)
            {
                foreach (IValue iv in (AnArray)res["Items"])
                {
                    if (iv is Map)
                    {
                        Map m = (Map)iv;
                        if (m["Type"].ToString() == "Item")
                        {
                            items.Add(SimianInventoryConnector.ItemFromMap(m, m_GroupsService));
                        }
                    }
                }
                return items;
            }
            throw new InventoryInaccessible();
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
                throw new InventoryFolderNotStored(folder.ID);
            }
        }
        public override void Update(InventoryFolder folder)
        {
            Add(folder);
        }

        public override void IncrementVersion(UUID PrincipalID, UUID folderID)
        {
            InventoryFolder folder = this[PrincipalID, folderID];
#warning TODO: check whether Simian has a IncrementVersion check
            folder.Version += 1;
            Update(folder);
        }

        public override void Move(UUID PrincipalID, UUID folderID, UUID toFolderID)
        {
            InventoryFolder folder = this[PrincipalID, folderID];
            folder.ParentFolderID = toFolderID;
            Add(folder);
        }

        public override void Delete(UUID PrincipalID, UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "RemoveInventoryNode";
            post["OwnerID"] = (string)PrincipalID;
            post["ItemID"] = (string)folderID;

            Map m = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryFolderNotStored(folderID);
            }
        }

        public override void Purge(UUID folderID)
        {
            throw new NotImplementedException();
        }

        public override void Purge(UUID PrincipalID, UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "PurgeInventoryFolder";
            post["OwnerID"] = (string)PrincipalID;
            post["FolderID"] = (string)folderID;

            Map m = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryFolderNotStored(folderID);
            }
        }
        #endregion
    }
}
