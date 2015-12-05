// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
    public sealed class RobustInventoryFolderConnector : InventoryFolderServiceInterface
    {
        readonly string m_InventoryURI;
        public int TimeoutMs = 20000;
        readonly GroupsServiceInterface m_GroupsService;
        readonly RobustInventoryFolderContentConnector m_FolderContentService;

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

        public override bool TryGetValue(UUID key, out InventoryFolder folder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ID"] = (string)key;
            post["METHOD"] = "GETFOLDER";
            Map map;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                folder = default(InventoryFolder);
                return false;
            }

            Map foldermap = map["folder"] as Map;
            if (null == foldermap)
            {
                folder = default(InventoryFolder);
                return false;
            }

            folder = RobustInventoryConnector.FolderFromMap(foldermap);
            return true;
        }

        public override bool ContainsKey(UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ID"] = (string)key;
            post["METHOD"] = "GETFOLDER";
            Map map;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                return false;
            }

            Map foldermap = map["folder"] as Map;
            if (null == foldermap)
            {
                return false;
            }

            return true;
        }

        public override InventoryFolder this[UUID key]
        {
            get
            {
                InventoryFolder folder;
                if(!TryGetValue(key, out folder))
                {
                    throw new InventoryInaccessibleException();
                }

                return folder;
            }
        }

        public override bool TryGetValue(UUID principalID, UUID key, out InventoryFolder folder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["ID"] = (string)key;
            post["METHOD"] = "GETFOLDER";
            Map map;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                folder = default(InventoryFolder);
                return false;
            }

            Map foldermap = map["folder"] as Map;
            if (null == foldermap)
            {
                folder = default(InventoryFolder);
                return false;
            }

            folder = RobustInventoryConnector.FolderFromMap(foldermap);
            return true;
        }

        public override bool ContainsKey(UUID principalID, UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["ID"] = (string)key;
            post["METHOD"] = "GETFOLDER";
            Map map;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                return false;
            }

            Map foldermap = map["folder"] as Map;
            return (null != foldermap);
        }

        public override InventoryFolder this[UUID principalID, UUID key]
        {
            get
            {
                InventoryFolder folder;
                if(!TryGetValue(principalID, key, out folder))
                {
                    throw new InventoryInaccessibleException();
                }
                return folder;
            }
        }

        public override bool TryGetValue(UUID principalID, AssetType type, out InventoryFolder folder)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            if (type == AssetType.RootFolder)
            {
                post["METHOD"] = "GETROOTFOLDER";
            }
            else
            {
                post["TYPE"] = ((int)type).ToString();
                post["METHOD"] = "GETFOLDERFORTYPE";
            }
            Map map;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }

            Map foldermap = map["folder"] as Map;
            if (null == foldermap)
            {
                folder = default(InventoryFolder);
                return false;
            }

            folder = RobustInventoryConnector.FolderFromMap(foldermap);
            return true;
        }

        public override bool ContainsKey(UUID principalID, AssetType type)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            if (type == AssetType.RootFolder)
            {
                post["METHOD"] = "GETROOTFOLDER";
            }
            else
            {
                post["TYPE"] = ((int)type).ToString();
                post["METHOD"] = "GETFOLDERFORTYPE";
            }
            Map map;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }

            Map foldermap = map["folder"] as Map;
            return (null != foldermap);
        }

        public override InventoryFolder this[UUID principalID, AssetType type]
        {
            get
            {
                InventoryFolder folder;
                if(!TryGetValue(principalID, type, out folder))
                {
                    throw new InventoryInaccessibleException();
                }
                return folder;
            }
        }

        public override List<InventoryFolder> GetFolders(UUID principalID, UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["FOLDER"] = (string)key;
            post["METHOD"] = "GETFOLDERCONTENT";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }

            Map foldersmap = map["FOLDERS"] as Map;
            if (null == foldersmap)
            {
                throw new InventoryInaccessibleException();
            }

            List<InventoryFolder> items = new List<InventoryFolder>();
            foreach (KeyValuePair<string, IValue> i in foldersmap)
            {
                Map folderdata = i.Value as Map;
                if (null != folderdata)
                {
                    items.Add(RobustInventoryConnector.FolderFromMap(folderdata));
                }
            }
            return items;
        }

        public override List<InventoryItem> GetItems(UUID principalID, UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["FOLDER"] = (string)key;
            post["METHOD"] = "GETFOLDERITEMS";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            Map itemsmap = map["ITEMS"] as Map;
            if(null == itemsmap)
            {
                throw new InventoryInaccessibleException();
            }

            List<InventoryItem> items = new List<InventoryItem>();
            foreach (KeyValuePair<string, IValue> i in itemsmap)
            {
                Map itemdata = i.Value as Map;
                if (null != itemdata)
                {
                    items.Add(RobustInventoryConnector.ItemFromMap(itemdata, m_GroupsService));
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
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folder.ID);
            }
        }
        public override void Update(InventoryFolder folder)
        {
            Dictionary<string, string> post = SerializeFolder(folder);
            post["METHOD"] = "UPDATEFOLDER";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folder.ID);
            }
        }

        public override void Move(UUID principalID, UUID folderID, UUID toFolderID)
        {
            Dictionary<string, string> post = new Dictionary<string,string>();
            post["ParentID"] = (string)toFolderID;
            post["ID"] = (string)folderID;
            post["PRINCIPAL"] = (string)principalID;
            post["METHOD"] = "MOVEFOLDER";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }

        public override void IncrementVersion(UUID principalID, UUID folderID)
        {
#warning TODO: race condition here with FolderVersion, needs a checkup against Robust HTTP API xinventory
            InventoryFolder folder = this[principalID, folderID];
            folder.Version += 1;
            Update(folder);
        }


        public override void Delete(UUID principalID, UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["FOLDERS[]"] = (string)folderID;
            post["PRINCIPAL"] = (string)principalID;
            post["METHOD"] = "DELETEFOLDERS";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }

        public override void Purge(UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ID"] = (string)folderID;
            post["METHOD"] = "PURGEFOLDER";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }

        public override void Purge(UUID principalID, UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["ID"] = (string)folderID;
            post["PRINCIPAL"] = (string)principalID;
            post["METHOD"] = "PURGEFOLDER";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }
        #endregion
    }
}
