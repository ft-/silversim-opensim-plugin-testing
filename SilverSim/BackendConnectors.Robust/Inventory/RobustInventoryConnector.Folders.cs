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
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    public partial class RobustInventoryConnector : IInventoryFolderServiceInterface
    {
        #region Accessors
        IInventoryFolderContentServiceInterface IInventoryFolderServiceInterface.Content => this;

        bool IInventoryFolderServiceInterface.TryGetValue(UUID key, out InventoryFolder folder)
        {
            var post = new Dictionary<string, string>
            {
                ["ID"] = (string)key,
                ["METHOD"] = "GETFOLDER"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                folder = default(InventoryFolder);
                return false;
            }

            var foldermap = map["folder"] as Map;
            if (foldermap == null)
            {
                folder = default(InventoryFolder);
                return false;
            }

            folder = FolderFromMap(foldermap);
            return true;
        }

        bool IInventoryFolderServiceInterface.ContainsKey(UUID key)
        {
            var post = new Dictionary<string, string>
            {
                ["ID"] = (string)key,
                ["METHOD"] = "GETFOLDER"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                return false;
            }

            var foldermap = map["folder"] as Map;
            if (foldermap == null)
            {
                return false;
            }

            return true;
        }

        InventoryFolder IInventoryFolderServiceInterface.this[UUID key]
        {
            get
            {
                InventoryFolder folder;
                if(!Folder.TryGetValue(key, out folder))
                {
                    throw new InventoryFolderNotFoundException(key);
                }

                return folder;
            }
        }

        bool IInventoryFolderServiceInterface.TryGetValue(UUID principalID, UUID key, out InventoryFolder folder)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["ID"] = (string)key,
                ["METHOD"] = "GETFOLDER"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                folder = default(InventoryFolder);
                return false;
            }

            var foldermap = map["folder"] as Map;
            if (foldermap == null)
            {
                folder = default(InventoryFolder);
                return false;
            }

            folder = FolderFromMap(foldermap);
            return true;
        }

        bool IInventoryFolderServiceInterface.ContainsKey(UUID principalID, UUID key)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["ID"] = (string)key,
                ["METHOD"] = "GETFOLDER"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                return false;
            }

            var foldermap = map["folder"] as Map;
            return foldermap != null;
        }

        InventoryFolder IInventoryFolderServiceInterface.this[UUID principalID, UUID key]
        {
            get
            {
                InventoryFolder folder;
                if(!Folder.TryGetValue(principalID, key, out folder))
                {
                    throw new InventoryFolderNotFoundException(key);
                }
                return folder;
            }
        }

        bool IInventoryFolderServiceInterface.TryGetValue(UUID principalID, AssetType type, out InventoryFolder folder)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID
            };
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
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }

            var foldermap = map["folder"] as Map;
            if (foldermap == null)
            {
                folder = default(InventoryFolder);
                return false;
            }

            folder = FolderFromMap(foldermap);
            return true;
        }

        bool IInventoryFolderServiceInterface.ContainsKey(UUID principalID, AssetType type)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID
            };
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
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }

            var foldermap = map["folder"] as Map;
            return foldermap != null;
        }

        InventoryFolder IInventoryFolderServiceInterface.this[UUID principalID, AssetType type]
        {
            get
            {
                InventoryFolder folder;
                if(!Folder.TryGetValue(principalID, type, out folder))
                {
                    throw new InventoryFolderTypeNotFoundException(type);
                }
                return folder;
            }
        }

        List<InventoryFolder> IInventoryFolderServiceInterface.GetFolders(UUID principalID, UUID key)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["FOLDER"] = (string)key,
                ["METHOD"] = "GETFOLDERCONTENT"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }

            var foldersmap = map["FOLDERS"] as Map;
            if (foldersmap == null)
            {
                throw new InventoryInaccessibleException();
            }

            var items = new List<InventoryFolder>();
            foreach (KeyValuePair<string, IValue> i in foldersmap)
            {
                var folderdata = i.Value as Map;
                if (folderdata != null)
                {
                    items.Add(FolderFromMap(folderdata));
                }
            }
            return items;
        }

        List<InventoryItem> IInventoryFolderServiceInterface.GetItems(UUID principalID, UUID key)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["FOLDER"] = (string)key,
                ["METHOD"] = "GETFOLDERITEMS"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            var itemsmap = map["ITEMS"] as Map;
            if(itemsmap == null)
            {
                throw new InventoryInaccessibleException();
            }

            var items = new List<InventoryItem>();
            foreach (KeyValuePair<string, IValue> i in itemsmap)
            {
                var itemdata = i.Value as Map;
                if (itemdata != null)
                {
                    items.Add(ItemFromMap(itemdata, m_GroupsService));
                }
            }
            return items;
        }

        #endregion

        #region Methods
        private Dictionary<string, string> SerializeFolder(InventoryFolder folder) => new Dictionary<string, string>
        {
            ["ID"] = (string)folder.ID,
            ["ParentID"] = (string)folder.ParentFolderID,
            ["Type"] = ((int)folder.DefaultType).ToString(),
            ["Version"] = folder.Version.ToString(),
            ["Name"] = folder.Name,
            ["Owner"] = (string)folder.Owner.ID
        };

        void IInventoryFolderServiceInterface.Add(InventoryFolder folder)
        {
            Dictionary<string, string> post = SerializeFolder(folder);
            post["METHOD"] = "ADDFOLDER";
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
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
                throw new InventoryFolderNotStoredException(folder.ID);
            }
        }

        void IInventoryFolderServiceInterface.Update(InventoryFolder folder)
        {
            Dictionary<string, string> post = SerializeFolder(folder);
            post["METHOD"] = "UPDATEFOLDER";
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folder.ID);
            }
        }

        InventoryTree IInventoryFolderServiceInterface.Copy(UUID principalID, UUID folderID, UUID toFolderID) =>
            CopyFolder(principalID, folderID, toFolderID);

        void IInventoryFolderServiceInterface.Move(UUID principalID, UUID folderID, UUID toFolderID)
        {
            var post = new Dictionary<string, string>
            {
                ["ParentID"] = (string)toFolderID,
                ["ID"] = (string)folderID,
                ["PRINCIPAL"] = (string)principalID,
                ["METHOD"] = "MOVEFOLDER"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
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
                throw new InventoryFolderNotStoredException(folderID);
            }
        }

        void IInventoryFolderServiceInterface.IncrementVersion(UUID principalID, UUID folderID)
        {
#warning TODO: race condition here with FolderVersion, needs a checkup against Robust HTTP API xinventory
            InventoryFolder folder = Folder[principalID, folderID];
            ++folder.Version;
            Folder.Update(folder);
        }

        void IInventoryFolderServiceInterface.Delete(UUID principalID, UUID folderID)
        {
            var post = new Dictionary<string, string>
            {
                ["FOLDERS[]"] = (string)folderID,
                ["PRINCIPAL"] = (string)principalID,
                ["METHOD"] = "DELETEFOLDERS"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }

        void IInventoryFolderServiceInterface.Purge(UUID folderID)
        {
            var post = new Dictionary<string, string>
            {
                ["ID"] = (string)folderID,
                ["METHOD"] = "PURGEFOLDER"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }

        void IInventoryFolderServiceInterface.Purge(UUID principalID, UUID folderID)
        {
            var post = new Dictionary<string, string>
            {
                ["ID"] = (string)folderID,
                ["PRINCIPAL"] = (string)principalID,
                ["METHOD"] = "PURGEFOLDER"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_InventoryURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!((AString)map["RESULT"]))
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }
        #endregion

        List<UUID> IInventoryFolderServiceInterface.Delete(UUID principalID, List<UUID> folderIDs)
        {
            var deleted = new List<UUID>();
            foreach (UUID id in folderIDs)
            {
                try
                {
                    Folder.Delete(principalID, id);
                    deleted.Add(id);
                }
                catch
                {
                    /* nothing to do here */
                }
            }

            return deleted;
        }
    }
}
