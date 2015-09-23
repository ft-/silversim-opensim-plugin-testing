// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    class RobustInventoryFolderConnector : InventoryFolderServiceInterface
    {
        private string m_InventoryURI;
        public int TimeoutMs = 20000;
        private GroupsServiceInterface m_GroupsService;
        private RobustInventoryFolderContentConnector m_FolderContentService;

        #region Constructor
        public RobustInventoryFolderConnector(string uri, GroupsServiceInterface groupsService)
        {
            m_GroupsService = groupsService;
            m_InventoryURI = uri;
            m_FolderContentService = new RobustInventoryFolderContentConnector(uri, groupsService);
        }
        #endregion

        #region Accessors
        public override InventoryFolderContentServiceInterface Content
        {
            get
            {
                return m_FolderContentService;
            }
        }

        public override InventoryFolder this[UUID key]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["ID"] = (string)key;
                post["METHOD"] = "GETFOLDER";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
                if (!map.ContainsKey("folder"))
                {
                    throw new InventoryInaccessible();
                }
                else if (!(map["folder"] is Map))
                {
                    throw new InventoryInaccessible();
                }

                return RobustInventoryConnector.FolderFromMap((Map)map["folder"]);
            }
        }

        public override InventoryFolder this[UUID PrincipalID, UUID key]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)PrincipalID;
                post["ID"] = (string)key;
                post["METHOD"] = "GETFOLDER";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
                if (!map.ContainsKey("folder"))
                {
                    throw new InventoryInaccessible();
                }
                else if (!(map["folder"] is Map))
                {
                    throw new InventoryInaccessible();
                }

                return RobustInventoryConnector.FolderFromMap((Map)map["folder"]);
            }
        }

        public override InventoryFolder this[UUID PrincipalID, AssetType type]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)PrincipalID;
                if (type == AssetType.RootFolder)
                {
                    post["METHOD"] = "GETROOTFOLDER";
                }
                else
                { 
                    post["TYPE"] = ((int)type).ToString();
                    post["METHOD"] = "GETFOLDERFORTYPE";
                }
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
                if (!(map["folder"] is Map))
                {
                    throw new InventoryInaccessible();
                }

                return RobustInventoryConnector.FolderFromMap((Map)map["folder"]);
            }
        }

        public override List<InventoryFolder> getFolders(UUID PrincipalID, UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)PrincipalID;
            post["FOLDER"] = (string)key;
            post["METHOD"] = "GETFOLDERCONTENT";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!(map["FOLDERS"] is Map))
            {
                throw new InventoryInaccessible();
            }

            List<InventoryFolder> items = new List<InventoryFolder>();
            foreach (KeyValuePair<string, IValue> i in (Map)map["FOLDERS"])
            {
                if (i.Value is Map)
                {
                    items.Add(RobustInventoryConnector.FolderFromMap((Map)i.Value));
                }
            }
            return items;
        }

        public override List<InventoryItem> getItems(UUID PrincipalID, UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)PrincipalID;
            post["FOLDER"] = (string)key;
            post["METHOD"] = "GETFOLDERITEMS";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!(map["ITEMS"] is Map))
            {
                throw new InventoryInaccessible();
            }

            List<InventoryItem> items = new List<InventoryItem>();
            foreach (KeyValuePair<string, IValue> i in (Map)map["ITEMS"])
            {
                if (i.Value is Map)
                {
                    items.Add(RobustInventoryConnector.ItemFromMap((Map)i.Value, m_GroupsService));
                }
            }
            return items;
        }

        #endregion

        #region Methods
        private Dictionary<string, string> SerializeFolder(InventoryFolder folder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ID"] = (string)folder.ID;
            post["ParentID"] = (string)folder.ParentFolderID;
            post["Type"] = ((int)folder.InventoryType).ToString();
            post["Version"] = folder.Version.ToString();
            post["Name"] = folder.Name;
            post["Owner"] = (string)folder.Owner.ID;
            return post;
        }

        public override void Add(InventoryFolder folder)
        {
            Dictionary<string, string> post = SerializeFolder(folder);
            post["METHOD"] = "ADDFOLDER";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStored(folder.ID);
            }
        }
        public override void Update(InventoryFolder folder)
        {
            Dictionary<string, string> post = SerializeFolder(folder);
            post["METHOD"] = "UPDATEFOLDER";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStored(folder.ID);
            }
        }

        public override void Move(UUID PrincipalID, UUID folderID, UUID toFolderID)
        {
            Dictionary<string, string> post = new Dictionary<string,string>();
            post["ParentID"] = (string)toFolderID;
            post["ID"] = (string)folderID;
            post["PRINCIPAL"] = (string)PrincipalID;
            post["METHOD"] = "MOVEFOLDER";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStored(folderID);
            }
        }

        public override void IncrementVersion(UUID PrincipalID, UUID folderID)
        {
#warning TODO: race condition here with FolderVersion, needs a checkup against Robust HTTP API xinventory
            InventoryFolder folder = this[PrincipalID, folderID];
            folder.Version += 1;
            Update(folder);
        }


        public override void Delete(UUID PrincipalID, UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["FOLDERS[]"] = (string)folderID;
            post["PRINCIPAL"] = (string)PrincipalID;
            post["METHOD"] = "DELETEFOLDERS";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStored(folderID);
            }
        }

        public override void Purge(UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ID"] = (string)folderID;
            post["METHOD"] = "PURGEFOLDER";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStored(folderID);
            }
        }

        public override void Purge(UUID PrincipalID, UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ID"] = (string)folderID;
            post["PRINCIPAL"] = (string)PrincipalID;
            post["METHOD"] = "PURGEFOLDER";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStored(folderID);
            }
        }
        #endregion
    }
}
