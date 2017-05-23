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
    public partial class RobustInventoryConnector : IInventoryFolderContentServiceInterface
    {
        private bool m_IsMultipeServiceSupported = true;

        #region Private duplicate (keeps InventoryFolderConnector from having a circular reference)
        InventoryFolder GetFolder(UUID principalID, UUID key)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["ID"] = (string)key,
                ["METHOD"] = "GETFOLDER"
            };
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                throw new InventoryInaccessibleException();
            }

            var foldermap = map["folder"] as Map;
            if (foldermap == null)
            {
                throw new InventoryInaccessibleException();
            }

            return RobustInventoryConnector.FolderFromMap(foldermap);
        }
        #endregion

        bool IInventoryFolderContentServiceInterface.TryGetValue(UUID principalID, UUID folderID, out InventoryFolderContent inventoryFolderContent)
        {
            try
            {
                inventoryFolderContent = Folder.Content[principalID, folderID];
                return true;
            }
            catch
            {
                inventoryFolderContent = default(InventoryFolderContent);
                return false;
            }
        }

        bool IInventoryFolderContentServiceInterface.ContainsKey(UUID principalID, UUID folderID)
        {
            var post = new Dictionary<string, string>
            {
                ["PRINCIPAL"] = (string)principalID,
                ["ID"] = (string)folderID,
                ["METHOD"] = "GETFOLDER"
            };
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
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

        InventoryFolderContent IInventoryFolderContentServiceInterface.this[UUID principalID, UUID folderID]
        {
            get 
            {
                var post = new Dictionary<string, string>
                {
                    ["PRINCIPAL"] = (string)principalID,
                    ["FOLDER"] = (string)folderID,
                    ["METHOD"] = "GETFOLDERCONTENT"
                };
                Map map;
                using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }

                var folderContent = new InventoryFolderContent()
                {
                    Owner = new UUI(principalID),
                    FolderID = folderID,
                    Version = 0
                };
                if (map.ContainsKey("VERSION"))
                {
                    folderContent.Version = map["VERSION"].AsInt;
                }
                else
                {
                    InventoryFolder folder = GetFolder(principalID, folderID);
                    folderContent.Version = folder.Version;
                }

                if (map.ContainsKey("FOLDERS"))
                {
                    var foldersmap = map["FOLDERS"] as Map;
                    if(null != foldersmap)
                    {
                        foreach (KeyValuePair<string, IValue> ifolder in foldersmap)
                        {
                            var folderdata = ifolder.Value as Map;
                            if (null != folderdata)
                            {
                                folderContent.Folders.Add(RobustInventoryConnector.FolderFromMap(folderdata));
                            }
                        }
                    }
                }
                if(map.ContainsKey("ITEMS"))
                {
                    var itemsmap = map["ITEMS"] as Map;
                    if (null != itemsmap)
                    {
                        foreach (KeyValuePair<string, IValue> i in itemsmap)
                        {
                            var itemdata = i.Value as Map;
                            if (null != itemdata)
                            {
                                folderContent.Items.Add(RobustInventoryConnector.ItemFromMap(itemdata, m_GroupsService));
                            }
                        }
                    }
                }
                return folderContent;
            }
        }

        List<InventoryFolderContent> GetContentInSingleRequests(UUID principalID, UUID[] folderIDs)
        {
            var res = new List<InventoryFolderContent>();
            foreach (UUID folder in folderIDs)
            {
                try
                {
                    res.Add(Folder.Content[principalID, folder]);
                }
                catch
                {
                    /* nothing that we should do here */
                }
            }

            return res;
        }


        List<InventoryFolderContent> IInventoryFolderContentServiceInterface.this[UUID principalID, UUID[] folderIDs]
        {
            get
            {
                if(folderIDs.Length == 0)
                {
                    return new List<InventoryFolderContent>();
                }

                /* when the service failed for being not supported, we do not even try it again in that case */
                if (!m_IsMultipeServiceSupported)
                {
                    return GetContentInSingleRequests(principalID, folderIDs);
                }

                var post = new Dictionary<string, string>
                {
                    ["PRINCIPAL"] = (string)principalID,
                    ["FOLDERS"] = string.Join(",", folderIDs),
                    ["COUNT"] = folderIDs.Length.ToString(), /* <- some redundancy here for whatever unknown reason, it could have been derived from FOLDERS anyways */
                    ["METHOD"] = "GETMULTIPLEFOLDERSCONTENT"
                };
                Map map;
                try
                {
                    using (Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
                    {
                        map = OpenSimResponse.Deserialize(s);
                    }
                }
                catch(HttpClient.BadHttpResponseException)
                {
                    m_IsMultipeServiceSupported = false;
                    return GetContentInSingleRequests(principalID, folderIDs);
                }
                catch(HttpException e)
                {
                    if(e.GetHttpCode() == (int)HttpStatusCode.BadGateway)
                    {
                        return GetContentInSingleRequests(principalID, folderIDs);
                    }
                    else
                    {
                        m_IsMultipeServiceSupported = false;
                        return GetContentInSingleRequests(principalID, folderIDs);
                    }
                }

                var contents = new List<InventoryFolderContent>();

                foreach(KeyValuePair<string, IValue> kvp in map)
                {
                    var fc = kvp.Value as Map;
                    if(kvp.Key.StartsWith("F_") && null != fc)
                    {
                        var folderContent = new InventoryFolderContent()
                        {
                            Owner = new UUI(fc["OWNER"].AsUUID),
                            FolderID = fc["FID"].AsUUID,
                            Version = fc["VERSION"].AsInt
                        };
                        if (map.ContainsKey("FOLDERS"))
                        {
                            var foldersmap = map["FOLDERS"] as Map;
                            if(null != foldersmap)
                            {
                                foreach (KeyValuePair<string, IValue> ifolder in foldersmap)
                                {
                                    var folderdata = ifolder.Value as Map;
                                    if (null != folderdata)
                                    {
                                        folderContent.Folders.Add(RobustInventoryConnector.FolderFromMap(folderdata));
                                    }
                                }
                            }
                        }

                        if (map.ContainsKey("ITEMS"))
                        {
                            var itemsmap = map["ITEMS"] as Map;
                            if(itemsmap != null)
                            {
                                foreach (KeyValuePair<string, IValue> i in itemsmap)
                                {
                                    var itemdata = i.Value as Map;
                                    if (null != itemdata)
                                    {
                                        folderContent.Items.Add(RobustInventoryConnector.ItemFromMap(itemdata, m_GroupsService));
                                    }
                                }
                            }
                        }
                    }
                }

                if(contents.Count == 0)
                {
                    /* try old method */
                    contents = GetContentInSingleRequests(principalID, folderIDs);
                    if(contents.Count > 0)
                    {
                        m_IsMultipeServiceSupported = false;
                    }
                }
                return contents;
            }
        }

    }
}
