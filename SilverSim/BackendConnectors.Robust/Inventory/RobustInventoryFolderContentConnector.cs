// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Inventory;
using System.Collections.Generic;
using System.Net;
using System.Web;

namespace SilverSim.BackendConnectors.Robust.Inventory
{
    public class RobustInventoryFolderContentConnector : InventoryFolderContentServiceInterface
    {
        bool m_IsMultipeServiceSupported = true;
        string m_InventoryURI;
        public int TimeoutMs = 20000;
        private GroupsServiceInterface m_GroupsService;

        public RobustInventoryFolderContentConnector(string url, GroupsServiceInterface groupsService)
        {
            m_InventoryURI = url;
            m_GroupsService = groupsService;
        }

        #region Private duplicate (keeps InventoryFolderConnector from having a circular reference)
        InventoryFolder GetFolder(UUID PrincipalID, UUID key)
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
        #endregion

        public override InventoryFolderContent this[UUID principalID, UUID folderID]
        {
            get 
            {
                InventoryFolderContent folderContent = new InventoryFolderContent();
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["PRINCIPAL"] = (string)principalID;
                post["FOLDER"] = (string)folderID;
                post["METHOD"] = "GETFOLDERCONTENT";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));

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

                if (map.ContainsKey("FOLDERS") && map["FOLDERS"] is Map)
                {
                    foreach (KeyValuePair<string, IValue> i in (Map)map["FOLDERS"])
                    {
                        if (i.Value is Map)
                        {
                            folderContent.Folders.Add(RobustInventoryConnector.FolderFromMap((Map)i.Value));
                        }
                    }
                }
                if(map.ContainsKey("ITEMS") && map["ITEMS"] is Map)
                {
                    foreach (KeyValuePair<string, IValue> i in (Map)map["ITEMS"])
                    {
                        if (i.Value is Map)
                        {
                            folderContent.Items.Add(RobustInventoryConnector.ItemFromMap((Map)i.Value, m_GroupsService));
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
                    map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_InventoryURI, null, post, false, TimeoutMs));
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
                    if(kvp.Key.StartsWith("F_") && kvp.Value is Map)
                    {
                        Map fc = (Map)kvp.Value;

                        InventoryFolderContent folderContent = new InventoryFolderContent();
                        folderContent.Owner.ID = fc["OWNER"].AsUUID;
                        folderContent.FolderID = fc["FID"].AsUUID;
                        folderContent.Version = fc["VERSION"].AsInt;

                        if (map.ContainsKey("FOLDERS") && map["FOLDERS"] is Map)
                        {
                            foreach (KeyValuePair<string, IValue> i in (Map)map["FOLDERS"])
                            {
                                if (i.Value is Map)
                                {
                                    folderContent.Folders.Add(RobustInventoryConnector.FolderFromMap((Map)i.Value));
                                }
                            }
                        }

                        if (map.ContainsKey("ITEMS") && map["ITEMS"] is Map)
                        {
                            foreach (KeyValuePair<string, IValue> i in (Map)map["ITEMS"])
                            {
                                if (i.Value is Map)
                                {
                                    folderContent.Items.Add(RobustInventoryConnector.ItemFromMap((Map)i.Value, m_GroupsService));
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
