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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Web;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
    public partial class RobustInventoryConnector : IInventoryFolderContentServiceInterface
    {
        bool m_IsMultipeServiceSupported = true;

        #region Private duplicate (keeps InventoryFolderConnector from having a circular reference)
        InventoryFolder GetFolder(UUID principalID, UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["ID"] = (string)key;
            post["METHOD"] = "GETFOLDER";
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("folder"))
            {
                throw new InventoryInaccessibleException();
            }

            Map foldermap = map["folder"] as Map;
            if (null == foldermap)
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["ID"] = (string)folderID;
            post["METHOD"] = "GETFOLDER";
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
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

        InventoryFolderContent IInventoryFolderContentServiceInterface.this[UUID principalID, UUID folderID]
        {
            get 
            {
                InventoryFolderContent folderContent = new InventoryFolderContent();
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)principalID;
                post["FOLDER"] = (string)folderID;
                post["METHOD"] = "GETFOLDERCONTENT";
                Map map;
                using(Stream s = HttpClient.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }

                folderContent.Owner.ID = principalID;
                folderContent.FolderID = folderID;
                folderContent.Version = 0;
                if(map.ContainsKey("VERSION"))
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
                    Map foldersmap = map["FOLDERS"] as Map;
                    if(null != foldersmap)
                    {
                        foreach (KeyValuePair<string, IValue> ifolder in foldersmap)
                        {
                            Map folderdata = ifolder.Value as Map;
                            if (null != folderdata)
                            {
                                folderContent.Folders.Add(RobustInventoryConnector.FolderFromMap(folderdata));
                            }
                        }
                    }
                }
                if(map.ContainsKey("ITEMS"))
                {
                    Map itemsmap = map["ITEMS"] as Map;
                    if (null != itemsmap)
                    {
                        foreach (KeyValuePair<string, IValue> i in itemsmap)
                        {
                            Map itemdata = i.Value as Map;
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

        [SuppressMessage("Gendarme.Rules.Design", "AvoidMultidimensionalIndexerRule")]
        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotSwallowErrorsCatchingNonSpecificExceptionsRule")]
        List<InventoryFolderContent> GetContentInSingleRequests(UUID principalID, UUID[] folderIDs)
        {
            List<InventoryFolderContent> res = new List<InventoryFolderContent>();
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

                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)principalID;
                post["FOLDERS"] = string.Join(",", folderIDs);
                post["COUNT"] = folderIDs.Length.ToString(); /* <- some redundancy here for whatever unknown reason, it could have been derived from FOLDERS anyways */
                post["METHOD"] = "GETMULTIPLEFOLDERSCONTENT";
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

                List<InventoryFolderContent> contents = new List<InventoryFolderContent>();

                foreach(KeyValuePair<string, IValue> kvp in map)
                {
                    Map fc = kvp.Value as Map;
                    if(kvp.Key.StartsWith("F_") && null != fc)
                    {
                        InventoryFolderContent folderContent = new InventoryFolderContent();
                        folderContent.Owner.ID = fc["OWNER"].AsUUID;
                        folderContent.FolderID = fc["FID"].AsUUID;
                        folderContent.Version = fc["VERSION"].AsInt;

                        if (map.ContainsKey("FOLDERS"))
                        {
                            Map foldersmap = map["FOLDERS"] as Map;
                            if(null != foldersmap)
                            {
                                foreach (KeyValuePair<string, IValue> ifolder in foldersmap)
                                {
                                    Map folderdata = ifolder.Value as Map;
                                    if (null != folderdata)
                                    {
                                        folderContent.Folders.Add(RobustInventoryConnector.FolderFromMap(folderdata));
                                    }
                                }
                            }
                        }

                        if (map.ContainsKey("ITEMS"))
                        {
                            Map itemsmap = map["ITEMS"] as Map;
                            if(null != itemsmap)
                            {
                                foreach (KeyValuePair<string, IValue> i in itemsmap)
                                {
                                    Map itemdata = i.Value as Map;
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
