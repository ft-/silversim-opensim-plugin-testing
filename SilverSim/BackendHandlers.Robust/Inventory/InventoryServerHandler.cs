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

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using SilverSim.Types.StructuredData.REST;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Inventory
{
    #region Internal Extension methods and exceptions

    internal static class ExtensionMethods
    {
        public static InventoryItem ToItem(this Dictionary<string, object> dict)
        {
            var item = new InventoryItem()
            {
                AssetID = dict.GetUUID("AssetID"),
                AssetType = (AssetType)dict.GetInt("AssetType"),
                Name = dict.GetString("Name"),
                Owner = new UUI(dict.GetUUID("Owner")),
                ID = dict.GetUUID("ID"),
                InventoryType = (InventoryType)dict.GetInt("InvType"),
                ParentFolderID = dict.GetUUID("Folder"),
                Creator = new UUI { ID = dict.GetUUID("CreatorId"), CreatorData = dict.GetString("CreatorData") },
                Description = dict.GetString("Description"),
                Group = new UGI(dict.GetUUID("GroupID")),
                IsGroupOwned = bool.Parse(dict.GetString("GroupOwned")),
                Flags = (InventoryFlags)dict.GetUInt("Flags"),
                CreationDate = Date.UnixTimeToDateTime(dict.GetULong("CreationDate"))
            };
            item.Permissions.NextOwner = (InventoryPermissionsMask)dict.GetUInt("NextPermissions");
            item.Permissions.Current = (InventoryPermissionsMask)dict.GetUInt("CurrentPermissions");
            item.Permissions.Base = (InventoryPermissionsMask)dict.GetUInt("BasePermissions");
            item.Permissions.EveryOne = (InventoryPermissionsMask)dict.GetUInt("EveryOnePermissions");
            item.Permissions.Group = (InventoryPermissionsMask)dict.GetUInt("groupPermissions");
            item.SaleInfo.Price = dict.GetInt("SalePrice");
            item.SaleInfo.Type = (InventoryItem.SaleInfoData.SaleType)dict.GetInt("SaleType");

            return item;
        }

        public static InventoryFolder ToFolder(this Dictionary<string, object> dict) => new InventoryFolder()
        {
            ParentFolderID = dict.GetUUID("ParentID"),
            InventoryType = (InventoryType)dict.GetInt("Type"),
            Version = dict.GetInt("Version"),
            Name = dict.GetString("Name"),
            Owner = new UUI(dict.GetUUID("Owner")),
            ID = dict.GetUUID("ID")
        };

        public static void WriteFolder(this XmlTextWriter writer, string name, InventoryFolder folder)
        {
            writer.WriteStartElement(name);
            writer.WriteAttributeString("type", "List");
            {
                writer.WriteNamedValue("ParentID", folder.ParentFolderID);
                writer.WriteNamedValue("Type", (int)folder.InventoryType);
                writer.WriteNamedValue("Version", folder.Version);
                writer.WriteNamedValue("Name", folder.Name);
                writer.WriteNamedValue("Owner", folder.Owner.ID);
                writer.WriteNamedValue("ID", folder.ID);
            }
            writer.WriteEndElement();
        }

        public static void WriteItem(this XmlTextWriter writer, string name, InventoryItem item)
        {
            writer.WriteStartElement(name);
            writer.WriteAttributeString("type", "List");
            {
                writer.WriteNamedValue("AssetID", item.AssetID);
                writer.WriteNamedValue("AssetType", (int)item.AssetType);
                writer.WriteNamedValue("BasePermissions", (uint)item.Permissions.Base);
                writer.WriteNamedValue("CreationDate", (long)item.CreationDate.DateTimeToUnixTime());
                writer.WriteNamedValue("CreatorId", item.Creator.ID);
                writer.WriteNamedValue("CreatorData", item.Creator.CreatorData);
                writer.WriteNamedValue("CurrentPermissions", (uint)item.Permissions.Current);
                writer.WriteNamedValue("Description", item.Description);
                writer.WriteNamedValue("EveryOnePermissions", (uint)item.Permissions.EveryOne);
                writer.WriteNamedValue("Flags", (uint)item.Flags);
                writer.WriteNamedValue("Folder", item.ParentFolderID);
                writer.WriteNamedValue("GroupID", item.Group.ID);
                writer.WriteNamedValue("GroupOwned", item.IsGroupOwned);
                writer.WriteNamedValue("GroupPermissions", (uint)item.Permissions.Group);
                writer.WriteNamedValue("ID", item.ID);
                writer.WriteNamedValue("InvType", (int)item.InventoryType);
                writer.WriteNamedValue("NextPermissions", (uint)item.Permissions.NextOwner);
                writer.WriteNamedValue("Owner", item.Owner.ID);
                writer.WriteNamedValue("SalePrice", item.SaleInfo.Price);
                writer.WriteNamedValue("SaleType", (byte)item.SaleInfo.Type);
            }
            writer.WriteEndElement();
        }

        public static void WriteFolderContent(this XmlTextWriter writer, string name, InventoryFolderContent content, bool serializeOwner = false)
        {
            if(!string.IsNullOrEmpty(name))
            {
                writer.WriteStartElement(name);
                writer.WriteAttributeString("type", "List");
            }

            {
                writer.WriteNamedValue("FID", content.FolderID);

                writer.WriteNamedValue("VERSION", content.Version);

                if(serializeOwner)
                {
                    writer.WriteNamedValue("OWNER", content.Owner.ID);
                }

                int count = 0;
                writer.WriteStartElement("FOLDERS");
                writer.WriteAttributeString("type", "List");
                foreach (InventoryFolder folder in content.Folders)
                {
                    writer.WriteFolder("folder_" + count.ToString(), folder);
                    ++count;
                }
                writer.WriteEndElement();

                count = 0;
                writer.WriteStartElement("ITEMS");
                writer.WriteAttributeString("type", "List");
                foreach (InventoryItem item in content.Items)
                {
                    writer.WriteItem("item_" + count.ToString(), item);
                    ++count;
                }
                writer.WriteEndElement();
            }

            if(!string.IsNullOrEmpty(name))
            {
                writer.WriteEndElement();
            }
        }
    }
    #endregion

    #region Service Implementation
    [Description("Robust Inventory Protocol Server")]
    [PluginName("InventoryHandler")]
    public class RobustInventoryServerHandler : IPlugin, IServiceURLsGetInterface
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST INVENTORY HANDLER");
        private BaseHttpServer m_HttpServer;
        private InventoryServiceInterface m_InventoryService;
        private readonly string m_InventoryServiceName;
        private readonly bool m_AdvertiseInventoryServerURI;
        private readonly Dictionary<string, Action<HttpRequest, Dictionary<string, object>>> m_Handlers = new Dictionary<string, Action<HttpRequest, Dictionary<string, object>>>();

        public RobustInventoryServerHandler(IConfig ownSection)
        {
            m_AdvertiseInventoryServerURI = ownSection.GetBoolean("AdvertiseServerURI", true);
            m_InventoryServiceName = ownSection.GetString("InventoryService", "InventoryService");
            m_Handlers["CREATEUSERINVENTORY"] = CreateUserInventory;
            m_Handlers["GETINVENTORYSKELETON"] = GetInventorySkeleton;
            m_Handlers["GETROOTFOLDER"] = GetRootFolder;
            m_Handlers["GETFOLDERFORTYPE"] = GetFolderForType;
            m_Handlers["GETFOLDERCONTENT"] = GetFolderContent;
            m_Handlers["GETMULTIPLEFOLDERSCONTENT"] = GetMultipleFoldersContent;
            m_Handlers["GETFOLDERITEMS"] = GetFolderItems;
            m_Handlers["ADDFOLDER"] = AddFolder;
            m_Handlers["UPDATEFOLDER"] = UpdateFolder;
            m_Handlers["MOVEFOLDER"] = MoveFolder;
            m_Handlers["DELETEFOLDERS"] = DeleteFolders;
            m_Handlers["PURGEFOLDER"] = PurgeFolder;
            m_Handlers["ADDITEM"] = AddItem;
            m_Handlers["UPDATEITEM"] = UpdateItem;
            m_Handlers["MOVEITEMS"] = MoveItems;
            m_Handlers["DELETEITEMS"] = DeleteItems;
            m_Handlers["GETITEM"] = GetItem;
            m_Handlers["GETMULTIPLEITEMS"] = GetMultipleItems;
            m_Handlers["GETFOLDER"] = GetFolder;
            m_Handlers["GETACTIVEGESTURES"] = GetActiveGestures;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for asset server");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.UriHandlers.Add("/xinventory", InventoryHandler);
            m_InventoryService = loader.GetService<InventoryServiceInterface>(m_InventoryServiceName);
            try
            {
                loader.HttpsServer.UriHandlers.Add("/xinventory", InventoryHandler);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        void IServiceURLsGetInterface.GetServiceURLs(Dictionary<string, string> dict)
        {
            dict["InventoryServerURI"] = m_HttpServer.ServerURI;
        }

        private void SuccessResult(HttpRequest httpreq)
        {
            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("RESULT");
                    writer.WriteValue(true);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
        }

        private void InventoryHandler(HttpRequest httpreq)
        {
            if (httpreq.ContainsHeader("X-SecondLife-Shard"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Request source not allowed");
                return;
            }

            if(httpreq.Method != "POST")
            {
                httpreq.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                return;
            }

            Dictionary<string, object> reqdata;
            try
            {
                reqdata = REST.ParseREST(httpreq.Body);
            }
            catch
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if(!reqdata.ContainsKey("METHOD"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Missing 'METHOD' field");
                return;
            }

            Action<HttpRequest, Dictionary<string, object>> del;
            try
            {
                if (m_Handlers.TryGetValue(reqdata["METHOD"].ToString(), out del))
                {
                    del(httpreq, reqdata);
                }
                else
                {
                    throw new FailureResultException();
                }
            }
            catch (FailureResultException)
            {
                using (HttpResponse res = httpreq.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteStartElement("RESULT");
                        writer.WriteValue(false);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
            }
            catch
            {
                if (httpreq.Response != null)
                {
                    httpreq.Response.Close();
                }
                else
                {
                    using (HttpResponse res = httpreq.BeginResponse("text/xml"))
                    {
                        using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                        {
                            writer.WriteStartElement("ServerResponse");
                            writer.WriteStartElement("RESULT");
                            writer.WriteValue(false);
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                    }
                }
            }
        }

        private void CreateUserInventory(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            try
            {
                m_InventoryService.CheckInventory(principalID);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        private void GetInventorySkeleton(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            List<InventoryFolder> folders;
            try
            {
                folders = m_InventoryService.GetInventorySkeleton(principalID);
            }
            catch
            {
                throw new FailureResultException();
            }
            int count = 0;
            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    foreach (InventoryFolder folder in folders)
                    {
                        writer.WriteFolder("folder_" + count.ToString(), folder);
                        ++count;
                    }
                    writer.WriteEndElement();
                }
            }
        }

        private void GetRootFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            InventoryFolder folder;
            try
            {
                folder = m_InventoryService.Folder[principalID, AssetType.RootFolder];
            }
            catch
            {
                throw new FailureResultException();
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteFolder("folder", folder);
                    writer.WriteEndElement();
                }
            }
        }

        private void GetFolderForType(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            AssetType type = (AssetType) reqdata.GetInt("TYPE");
            InventoryFolder folder;
            try
            {
                folder = m_InventoryService.Folder[principalID, type];
            }
            catch
            {
                throw new FailureResultException();
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteFolder("folder", folder);
                    writer.WriteEndElement();
                }
            }
        }

        private void GetFolderContent(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            UUID folderID = reqdata.GetUUID("FOLDER");

            InventoryFolderContent folder;
            try
            {
                folder = m_InventoryService.Folder.Content[principalID, folderID];
            }
            catch
            {
                throw new FailureResultException();
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteFolderContent(string.Empty, folder);
                    writer.WriteEndElement();
                }
            }
        }

        private void GetMultipleFoldersContent(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            string folderIDstring = reqdata.GetString("FOLDERS");
            string[] uuidstrs = folderIDstring.Split(',');
            UUID[] uuids = new UUID[uuidstrs.Length];

            for (int i = 0; i < uuidstrs.Length; ++i)
            {
                if(!UUID.TryParse(uuidstrs[i], out uuids[i]))
                {
                    throw new FailureResultException();
                }
            }

            List<InventoryFolderContent> foldercontents;
            try
            {
                foldercontents = m_InventoryService.Folder.Content[principalID, uuids];
            }
            catch
            {
                throw new FailureResultException();
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    foreach (InventoryFolderContent content in foldercontents)
                    {
                        writer.WriteFolderContent("F_" + content.FolderID.ToString(), content, true);
                    }
                    writer.WriteEndElement();
                }
            }
        }

        private void GetFolderItems(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            UUID folderID = reqdata.GetUUID("FOLDER");

            List<InventoryItem> folderitems;
            try
            {
                folderitems = m_InventoryService.Folder.GetItems(principalID, folderID);
            }
            catch
            {
                throw new FailureResultException();
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    int count = 0;
                    writer.WriteStartElement("ITEMS");
                    foreach (InventoryItem item in folderitems)
                    {
                        writer.WriteItem("item_" + count.ToString(), item);
                        ++count;
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
        }

        private void AddFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            InventoryFolder folder = reqdata.ToFolder();

            try
            {
                m_InventoryService.Folder.Add(folder);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        private void UpdateFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            InventoryFolder folder = reqdata.ToFolder();

            try
            {
                m_InventoryService.Folder.Update(folder);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        private void MoveFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID parentID = reqdata.GetUUID("ParentID");
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            UUID folderID = reqdata.GetUUID("ID");

            try
            {
                m_InventoryService.Folder.Move(principalID, folderID, parentID);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        private void DeleteFolders(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            List<UUID> folderIDs = reqdata.GetUUIDList("FOLDERS");

            try
            {
                m_InventoryService.Folder.Delete(principalID, folderIDs);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        private void PurgeFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID folderID = reqdata.GetUUID("ID");
            if (reqdata.ContainsKey("PRINCIPAL")) /* OpenSim is not sending this. So, we have to be prepared for that on HG. */
            {
                UUID principalID = reqdata.GetUUID("PRINCIPAL");
                try
                {
                    m_InventoryService.Folder.Purge(principalID, folderID);
                }
                catch
                {
                    throw new FailureResultException();
                }
                SuccessResult(httpreq);
            }
            else
            {
                try
                {
                    m_InventoryService.Folder.Purge(folderID);
                }
                catch
                {
                    throw new FailureResultException();
                }
                SuccessResult(httpreq);
            }
        }

        private void AddItem(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            InventoryItem item = reqdata.ToItem();

            try
            {
                m_InventoryService.Item.Add(item);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        private void UpdateItem(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            InventoryItem item = reqdata.ToItem();

            try
            {
                m_InventoryService.Item.Update(item);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        private void MoveItems(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            List<UUID> idList = reqdata.GetUUIDList("IDLIST");
            List<UUID> destList = reqdata.GetUUIDList("DESTLIST");
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            if(idList.Count != destList.Count)
            {
                throw new FailureResultException();
            }

            try
            {
                for(int i = 0; i < idList.Count; ++i)
                {
                    m_InventoryService.Item.Move(principalID, idList[i], destList[i]);
                }
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        private void DeleteItems(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            List<UUID> idList = reqdata.GetUUIDList("ITEMS");
            try
            {
                m_InventoryService.Item.Delete(principalID, idList);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        private void GetItem(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID itemID = reqdata.GetUUID("ID");
            InventoryItem item;
            if (reqdata.ContainsKey("PRINCIPAL")) /* OpenSim is not sending this. So, we have to be prepared for that on HG. */
            {
                UUID principalID = reqdata.GetUUID("PRINCIPAL");
                try
                {
                    item = m_InventoryService.Item[principalID, itemID];
                }
                catch
                {
                    throw new FailureResultException();
                }
            }
            else
            {
                try
                {
                    item = m_InventoryService.Item[itemID];
                }
                catch
                {
                    throw new FailureResultException();
                }
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteItem("item", item);
                    writer.WriteEndElement();
                }
            }
        }

        private void GetMultipleItems(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            string itemIDstring = reqdata.GetString("ITEMS");
            string[] uuidstrs = itemIDstring.Split(',');
            var uuids = new UUID[uuidstrs.Length];

            List<InventoryItem> items;
            try
            {
                items = m_InventoryService.Item[principalID, new List<UUID>(uuids)];
            }
            catch
            {
                throw new FailureResultException();
            }

            var keyeditems = new Dictionary<UUID,InventoryItem>();
            foreach(InventoryItem i in items)
            {
                keyeditems[i.ID] = i;
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                int count = 0;
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    foreach (UUID uuid in uuids)
                    {
                        InventoryItem item;
                        if (keyeditems.TryGetValue(uuid, out item))
                        {
                            writer.WriteItem("item_" + count.ToString(), item);
                        }
                        else
                        {
                            writer.WriteStartElement("item_" + count.ToString());
                            writer.WriteValue("NULL");
                            writer.WriteEndElement();
                        }
                        ++count;
                    }
                    writer.WriteEndElement();
                }
            }
        }

        private void GetFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID folderID = reqdata.GetUUID("ID");
            InventoryFolder folder;
            if (reqdata.ContainsKey("PRINCIPAL")) /* OpenSim is not sending this. So, we have to be prepared for that on HG. */
            {
                UUID principalID = reqdata.GetUUID("PRINCIPAL");
                try
                {
                    folder = m_InventoryService.Folder[principalID, folderID];
                }
                catch
                {
                    throw new FailureResultException();
                }
            }
            else
            {
                try
                {
                    folder = m_InventoryService.Folder[folderID];
                }
                catch
                {
                    throw new FailureResultException();
                }
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteFolder("folder", folder);
                    writer.WriteEndElement();
                }
            }
        }

        private void GetActiveGestures(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            List<InventoryItem> gestures;
            try
            {
                gestures = m_InventoryService.GetActiveGestures(principalID);
            }
            catch
            {
                throw new FailureResultException();
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                int count = 0;
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    foreach (InventoryItem item in gestures)
                    {
                        writer.WriteItem("item_" + count.ToString(), item);
                        ++count;
                    }
                    writer.WriteEndElement();
                }
            }
        }
    }
    #endregion
}
