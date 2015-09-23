// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Inventory
{
    #region Internal Extension methods and exceptions

    static class ExtensionMethods
    {
        public static InventoryItem ToItem(this Dictionary<string, object> dict)
        {
            InventoryItem item = new InventoryItem();
            item.AssetID = dict.GetUUID("AssetID");
            item.AssetType = (AssetType)dict.GetInt("AssetType");
            item.Name = dict.GetString("Name");
            item.Owner.ID = dict.GetUUID("Owner");
            item.ID = dict.GetUUID("ID");
            item.InventoryType = (InventoryType)dict.GetInt("InvType");
            item.ParentFolderID = dict.GetUUID("Folder");
            item.Creator.ID = dict.GetUUID("CreatorId");
            item.Creator.CreatorData = dict.GetString("CreatorData");
            item.Description = dict.GetString("Description");
            item.Permissions.NextOwner = (InventoryPermissionsMask)dict.GetUInt("NextPermissions");
            item.Permissions.Current = (InventoryPermissionsMask)dict.GetUInt("CurrentPermissions");
            item.Permissions.Base = (InventoryPermissionsMask)dict.GetUInt("BasePermissions");
            item.Permissions.EveryOne = (InventoryPermissionsMask)dict.GetUInt("EveryOnePermissions");
            item.Permissions.Group = (InventoryPermissionsMask)dict.GetUInt("groupPermissions");
            item.Group.ID = dict.GetUUID("GroupID");
            item.IsGroupOwned = bool.Parse(dict.GetString("GroupOwned"));
            item.SaleInfo.Price = dict.GetInt("SalePrice");
            item.SaleInfo.Type = (InventoryItem.SaleInfoData.SaleType)dict.GetInt("SaleType");
            item.Flags = dict.GetUInt("Flags");
            item.CreationDate = Date.UnixTimeToDateTime(dict.GetULong("CreationDate"));

            return item;
        }

        public static InventoryFolder ToFolder(this Dictionary<string, object> dict)
        {
            InventoryFolder folder = new InventoryFolder();
            folder.ParentFolderID = dict.GetUUID("ParentID");
            folder.InventoryType = (InventoryType)dict.GetInt("Type");
            folder.Version = dict.GetInt("Version");
            folder.Name = dict.GetString("Name");
            folder.Owner.ID = dict.GetUUID("Owner");
            folder.ID = dict.GetUUID("ID");
            return folder;
        }

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
    class RobustInventoryServerHandler : IPlugin
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST INVENTORY HANDLER");
        private BaseHttpServer m_HttpServer;
        private InventoryServiceInterface m_InventoryService;
        string m_InventoryServiceName;
        delegate void InventoryHandlerDelegate(HttpRequest httpreq, Dictionary<string, object> postVals);
        Dictionary<string, InventoryHandlerDelegate> m_Handlers = new Dictionary<string,InventoryHandlerDelegate>();

        public RobustInventoryServerHandler(string inventoryServiceName)
        {
            m_InventoryServiceName = inventoryServiceName;
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
            m_HttpServer.StartsWithUriHandlers.Add("/xinventory", InventoryHandler);
            m_InventoryService = loader.GetService<InventoryServiceInterface>(m_InventoryServiceName);
        }

        void SuccessResult(HttpRequest httpreq)
        {
            HttpResponse res = httpreq.BeginResponse("text/xml");
            using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteStartElement("RESULT");
                writer.WriteValue(true);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            res.Close();
        }

        void InventoryHandler(HttpRequest httpreq)
        {
            if (httpreq.ContainsHeader("X-SecondLife-Shard"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Request source not allowed");
                return;
            }

            if(httpreq.Method != "POST")
            {
                httpreq.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
                return;
            }

            Dictionary<string, object> reqdata;
            try
            {
                reqdata = REST.parseREST(httpreq.Body);
            }
            catch
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            if(!reqdata.ContainsKey("METHOD"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Missing 'METHOD' field");
                return;
            }

            InventoryHandlerDelegate del;
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
                HttpResponse res = httpreq.BeginResponse("text/xml");
                using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("RESULT");
                    writer.WriteValue(false);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                res.Close();
            }
            catch
            {
                if (httpreq.Response != null)
                {
                    httpreq.Response.Close();
                }
                else
                {
                    HttpResponse res = httpreq.BeginResponse("text/xml");
                    using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteStartElement("RESULT");
                        writer.WriteValue(false);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                    res.Close();
                }
            }
        }

        void CreateUserInventory(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            try
            {
                m_InventoryService.checkInventory(principalID);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(httpreq);
        }

        void GetInventorySkeleton(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            List<InventoryFolder> folders;
            try
            {
                folders = m_InventoryService.Folder.getInventorySkeleton(principalID);
            }
            catch
            {
                throw new FailureResultException();
            }
            int count = 0;
            HttpResponse res = httpreq.BeginResponse("text/xml");
            using(XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                foreach(InventoryFolder folder in folders)
                {
                    writer.WriteFolder("folder_" + count.ToString(), folder);
                    ++count;
                }
                writer.WriteEndElement();
            }
            res.Close();
        }

        void GetRootFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

            HttpResponse res = httpreq.BeginResponse("text/xml");
            using(XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteFolder("folder", folder);
                writer.WriteEndElement();
            }
            res.Close();
        }

        void GetFolderForType(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

            HttpResponse res = httpreq.BeginResponse("text/xml");
            using(XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteFolder("folder", folder);
                writer.WriteEndElement();
            }
            res.Close();
        }

        void GetFolderContent(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

            HttpResponse res = httpreq.BeginResponse("text/xml");
            using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteFolderContent(string.Empty, folder);
                writer.WriteEndElement();
            }
            res.Close();
        }

        void GetMultipleFoldersContent(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

            HttpResponse res = httpreq.BeginResponse("text/xml");
            using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                foreach(InventoryFolderContent content in foldercontents)
                {
                    writer.WriteFolderContent("F_" + content.FolderID, content, true);
                }
                writer.WriteEndElement();
            }
            res.Close();
        }

        void GetFolderItems(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            UUID folderID = reqdata.GetUUID("FOLDER");

            List<InventoryItem> folderitems;
            try
            {
                folderitems = m_InventoryService.Folder.getItems(principalID, folderID);
            }
            catch
            {
                throw new FailureResultException();
            }

            HttpResponse res = httpreq.BeginResponse("text/xml");
            using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
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
            res.Close();
        }

        void AddFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

        void UpdateFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

        void MoveFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

        void DeleteFolders(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

        void PurgeFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

        void AddItem(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

        void UpdateItem(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

        void MoveItems(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

        void DeleteItems(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

        void GetItem(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

            HttpResponse res = httpreq.BeginResponse("text/xml");
            using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteItem("item", item);
                writer.WriteEndElement();
            }
            res.Close();
        }

        void GetMultipleItems(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            string itemIDstring = reqdata.GetString("ITEMS");
            string[] uuidstrs = itemIDstring.Split(',');
            UUID[] uuids = new UUID[uuidstrs.Length];

            List<InventoryItem> items;
            try
            {
                items = m_InventoryService.Item[principalID, new List<UUID>(uuids)];
            }
            catch
            {
                throw new FailureResultException();
            }

            Dictionary<UUID, InventoryItem> keyeditems = new Dictionary<UUID,InventoryItem>();
            foreach(InventoryItem i in items)
            {
                keyeditems[i.ID] = i;
            }

            HttpResponse res = httpreq.BeginResponse("text/xml");
            int count = 0;
            using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                foreach(UUID uuid in uuids)
                {
                    InventoryItem item;
                    if(keyeditems.TryGetValue(uuid, out item))
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
            res.Close();
        }

        void GetFolder(HttpRequest httpreq, Dictionary<string, object> reqdata)
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

            HttpResponse res = httpreq.BeginResponse("text/xml");
            using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteFolder("folder", folder);
                writer.WriteEndElement();
            }
            res.Close();
        }

        void GetActiveGestures(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID = reqdata.GetUUID("PRINCIPAL");
            List<InventoryItem> gestures;
            try
            {
                gestures = m_InventoryService.getActiveGestures(principalID);
            }
            catch
            {
                throw new FailureResultException();
            }

            HttpResponse res = httpreq.BeginResponse("text/xml");
            int count = 0;
            using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                foreach (InventoryItem item in gestures)
                {
                    writer.WriteItem("item_" + count.ToString(), item);
                    ++count;
                }
                writer.WriteEndElement();
            }
            res.Close();
        }

        static UTF8Encoding UTF8NoBOM = new UTF8Encoding(false);
    }
    #endregion

    #region Factory
    [PluginName("InventoryHandler")]
    public class RobustInventoryServerHandlerFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST INVENTORY HANDLER");
        public RobustInventoryServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustInventoryServerHandler(ownSection.GetString("InventoryService", "InventoryService"));
        }
    }
    #endregion
}
