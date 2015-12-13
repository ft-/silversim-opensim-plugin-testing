// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Inventory;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Web;
using System;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
    public sealed class RobustInventoryFolderContentConnector : InventoryFolderContentServiceInterface
    {
        bool m_IsMultipeServiceSupported = true;
        readonly string m_InventoryURI;
        public int TimeoutMs = 20000;
        readonly GroupsServiceInterface m_GroupsService;

        public RobustInventoryFolderContentConnector(string url, GroupsServiceInterface groupsService)
        {
            m_InventoryURI = url;
            m_GroupsService = groupsService;
        }

        #region Private duplicate (keeps InventoryFolderConnector from having a circular reference)
        InventoryFolder GetFolder(UUID principalID, UUID key)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["ID"] = (string)key;
            post["METHOD"] = "GETFOLDER";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
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

        public override bool TryGetValue(UUID principalID, UUID folderID, out InventoryFolderContent inventoryFolderContent)
        {
            try
            {
                inventoryFolderContent = this[principalID, folderID];
                return true;
            }
            catch
            {
                inventoryFolderContent = default(InventoryFolderContent);
                return false;
            }
        }

        public override bool ContainsKey(UUID principalID, UUID folderID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PRINCIPAL"] = (string)principalID;
            post["ID"] = (string)folderID;
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

        public override InventoryFolderContent this[UUID principalID, UUID folderID]
        {
            get 
            {
                InventoryFolderContent folderContent = new InventoryFolderContent();
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)principalID;
                post["FOLDER"] = (string)folderID;
                post["METHOD"] = "GETFOLDERCONTENT";
                Map map;
                using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
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

        public override List<InventoryFolderContent> this[UUID principalID, UUID[] folderIDs]
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
                    return base[principalID, folderIDs];
                }

                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)principalID;
                post["FOLDERS"] = string.Join(",", folderIDs);
                post["COUNT"] = folderIDs.Length.ToString(); /* <- some redundancy here for whatever unknown reason, it could have been derived from FOLDERS anyways */
                post["METHOD"] = "GETMULTIPLEFOLDERSCONTENT";
                Map map;
                try
                {
                    using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs))
                    {
                        map = OpenSimResponse.Deserialize(s);
                    }
                }
                catch(HttpRequestHandler.BadHttpResponseException)
                {
                    m_IsMultipeServiceSupported = false;
                    return base[principalID, folderIDs];
                }
                catch(HttpException e)
                {
                    if(e.GetHttpCode() == (int)HttpStatusCode.BadGateway)
                    {
                        return base[principalID, folderIDs];
                    }
                    else
                    {
                        m_IsMultipeServiceSupported = false;
                        return base[principalID, folderIDs];
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
                    contents = base[principalID, folderIDs];
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
