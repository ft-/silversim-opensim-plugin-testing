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
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Simian.Inventory
{
    public partial class SimianInventoryConnector : IInventoryFolderServiceInterface
    {
        private readonly string m_SimCapability;

        #region Accessors
        bool IInventoryFolderServiceInterface.TryGetValue(UUID principalID, UUID key, out InventoryFolder invfolder)
        {
            foreach (InventoryFolder folder in Folder.GetFolders(principalID, key))
            {
                if (folder.ID.Equals(key))
                {
                    invfolder = folder;
                    return true;
                }
            }
            invfolder = default(InventoryFolder);
            return false;
        }

        bool IInventoryFolderServiceInterface.ContainsKey(UUID principalID, UUID key)
        {
            foreach (InventoryFolder folder in Folder.GetFolders(principalID, key))
            {
                if (folder.ID.Equals(key))
                {
                    return true;
                }
            }
            return false;
        }

        InventoryFolder IInventoryFolderServiceInterface.this[UUID principalID, UUID key]
        {
            get
            {
                InventoryFolder folder;
                if (!Folder.TryGetValue(principalID, key, out folder))
                {
                    throw new InventoryInaccessibleException();
                }

                return folder;
            }
        }

        bool IInventoryFolderServiceInterface.TryGetValue(UUID key, out InventoryFolder folder)
        {
            throw new NotSupportedException();
        }

        bool IInventoryFolderServiceInterface.ContainsKey(UUID key)
        {
            throw new NotSupportedException();
        }

        InventoryFolder IInventoryFolderServiceInterface.this[UUID key]
        {
            get { throw new NotSupportedException(); }
        }

        bool IInventoryFolderServiceInterface.TryGetValue(UUID principalID, AssetType type, out InventoryFolder folder)
        {
            var post = new Dictionary<string, string>();
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
                post["ContentType"] = ContentTypeFromAssetType(type);
            }
            Map res = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items"))
            {
                var resarray = res["Items"] as AnArray;
                if (resarray != null && resarray.Count != 0)
                {
                    var m = resarray[0] as Map;
                    if (m != null)
                    {
                        folder = FolderFromMap(m);
                        return true;
                    }
                }
            }
            folder = default(InventoryFolder);
            return false;
        }

        bool IInventoryFolderServiceInterface.ContainsKey(UUID principalID, AssetType type)
        {
            var post = new Dictionary<string, string>();
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
                var resarray = res["Items"] as AnArray;
                if (resarray != null && resarray.Count != 0)
                {
                    var m = resarray[0] as Map;
                    if (m != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        InventoryFolder IInventoryFolderServiceInterface.this[UUID principalID, AssetType type]
        {
            get
            {
                InventoryFolder folder;
                if(!Folder.TryGetValue(principalID, type, out folder))
                {
                    throw new InventoryInaccessibleException();
                }
                return folder;
            }
        }

        List<InventoryFolder> IInventoryFolderServiceInterface.GetFolders(UUID principalID, UUID key)
        {
            var folders = new List<InventoryFolder>();
            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "GetInventoryNode",
                ["ItemID"] = (string)key,
                ["OwnerID"] = (string)principalID,
                ["IncludeFolders"] = "1",
                ["IncludeItems"] = "0",
                ["ChildrenOnly"] = "1"
            };
            Map res = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items"))
            {
                var resarray = res["Items"] as AnArray;
                if(resarray != null)
                {
                    foreach (IValue iv in resarray)
                    {
                        var m = iv as Map;
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

        List<InventoryItem> IInventoryFolderServiceInterface.GetItems(UUID principalID, UUID key)
        {
            var items = new List<InventoryItem>();
            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "GetInventoryNode",
                ["ItemID"] = (string)key,
                ["OwnerID"] = (string)principalID,
                ["IncludeFolders"] = "0",
                ["IncludeItems"] = "1",
                ["ChildrenOnly"] = "1"
            };
            Map res = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Items"))
            {
                var resarray = res["Items"] as AnArray;
                if(resarray != null)
                {
                    foreach (IValue iv in resarray)
                    {
                        Map m = iv as Map;
                        if (m != null && m["Type"].ToString() == "Item")
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

        void IInventoryFolderServiceInterface.Add(InventoryFolder folder)
        {
            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "AddInventoryFolder",
                ["FolderID"] = (string)folder.ID,
                ["ParentID"] = (string)folder.ParentFolderID,
                ["ContentType"] = ((int)folder.InventoryType).ToString(),
                ["Name"] = folder.Name,
                ["OwnerID"] = (string)folder.Owner.ID
            };
            Map m = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                throw new InventoryFolderNotStoredException(folder.ID);
            }
        }

        void IInventoryFolderServiceInterface.Update(InventoryFolder folder)
        {
            Folder.Add(folder);
        }

        void IInventoryFolderServiceInterface.IncrementVersion(UUID principalID, UUID folderID)
        {
            InventoryFolder folder = Folder[principalID, folderID];
#warning TODO: check whether Simian has a IncrementVersion check
            ++folder.Version;
            Folder.Update(folder);
        }

        void IInventoryFolderServiceInterface.Move(UUID principalID, UUID folderID, UUID toFolderID)
        {
            InventoryFolder folder = Folder[principalID, folderID];
            folder.ParentFolderID = toFolderID;
            Folder.Add(folder);
        }

        void IInventoryFolderServiceInterface.Delete(UUID principalID, UUID folderID)
        {
            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "RemoveInventoryNode",
                ["OwnerID"] = (string)principalID,
                ["ItemID"] = (string)folderID
            };
            Map m = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
            {
                throw new InventoryFolderNotStoredException(folderID);
            }
        }

        void IInventoryFolderServiceInterface.Purge(UUID folderID)
        {
            throw new NotImplementedException();
        }

        void IInventoryFolderServiceInterface.Purge(UUID principalID, UUID folderID)
        {
            var post = new Dictionary<string, string>
            {
                ["RequestMethod"] = "PurgeInventoryFolder",
                ["OwnerID"] = (string)principalID,
                ["FolderID"] = (string)folderID
            };
            Map m = SimianGrid.PostToService(m_InventoryURI, m_SimCapability, post, TimeoutMs);
            if(!m["Success"].AsBoolean)
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
