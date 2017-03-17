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
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.StructuredData.AssetXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Asset
{
    #region Service Implementation
    [Description("Robust Asset Protocol Server")]
    public class RobustAssetServerHandler : IPlugin, IServiceURLsGetInterface
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST ASSET HANDLER");
        private BaseHttpServer m_HttpServer;
        AssetServiceInterface m_TemporaryAssetService;
        AssetServiceInterface m_PersistentAssetService;
        AssetServiceInterface m_ResourceAssetService;
        readonly string m_TemporaryAssetServiceName;
        readonly string m_PersistentAssetServiceName;
        readonly bool m_EnableGet;
        readonly bool m_EnableLimitedGet;

        public RobustAssetServerHandler(string persistentAssetServiceName, string temporaryAssetServiceName, bool enableGet, bool enableLimitedGet)
        {
            m_PersistentAssetServiceName = persistentAssetServiceName;
            m_TemporaryAssetServiceName = temporaryAssetServiceName;
            m_EnableGet = enableGet;
            m_EnableLimitedGet = enableLimitedGet;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for asset server");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.StartsWithUriHandlers.Add("/assets", AssetHandler);
            m_HttpServer.UriHandlers.Add("/get_assets_exist", GetAssetsExistHandler);
            m_PersistentAssetService = loader.GetService<AssetServiceInterface>(m_PersistentAssetServiceName);
            if (!string.IsNullOrEmpty(m_TemporaryAssetServiceName))
            {
                m_TemporaryAssetService = loader.GetService<AssetServiceInterface>(m_TemporaryAssetServiceName);
            }
            m_ResourceAssetService = loader.GetService<AssetServiceInterface>("ResourceAssetService");
            try
            {
                loader.HttpsServer.StartsWithUriHandlers.Add("/assets", AssetHandler);
                loader.HttpsServer.UriHandlers.Add("/get_assets_exist", GetAssetsExistHandler);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        void IServiceURLsGetInterface.GetServiceURLs(Dictionary<string, string> dict)
        {
            dict["AssetServerURI"] = m_HttpServer.ServerURI;
        }

        void AssetHandler(HttpRequest req)
        {
            if (req.ContainsHeader("X-SecondLife-Shard"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Request source not allowed");
                return;
            }

            switch (req.Method)
            {
                case "GET":
                    GetAssetHandler(req);
                    break;

                case "POST":
                    PostAssetHandler(req);
                    break;

                case "DELETE":
                    DeleteAssetHandler(req);
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                    break;
            }
        }

        private const int MAX_ASSET_BASE64_CONVERSION_SIZE = 9 * 1024; /* must be an integral multiple of 3 */

        void GetAssetHandler(HttpRequest req)
        {
            string uri;
            uri = req.RawUrl.Trim(new char[] { '/' });
            string[] parts = uri.Split('/');
            if (parts.Length < 2)
            {
                req.ErrorResponse(HttpStatusCode.NotFound);
                return;
            }

            UUID id;
            try
            {
                id = new UUID(parts[1]);
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.NotFound);
                return;
            }

            if(!m_EnableGet && !m_EnableLimitedGet)
            {
                req.ErrorResponse(HttpStatusCode.NotFound);
                return;
            }

            if(parts.Length == 2)
            {
                GetAssetHandler_Xml(req, id);
            }
            else if(parts[2] == "metadata")
            {
                GetAssetHandler_Metadata(req, id);
            }
            else if(parts[2] == "data")
            {
                GetAssetHandler_Data(req, id);
            }
            else
            {
                req.ErrorResponse(HttpStatusCode.NotFound);
            }
        }

        void GetAssetHandler_Xml(HttpRequest req, UUID id)
        {
            AssetData data;
            try
            {
                data = m_TemporaryAssetService[id];
            }
            catch
            {
                try
                {
                    data = m_PersistentAssetService[id];
                }
                catch
                {
                    try
                    {
                        data = m_ResourceAssetService[id];
                    }
                    catch
                    {
                        req.ErrorResponse(HttpStatusCode.NotFound);
                        return;
                    }
                }
            }

            if (!m_EnableGet)
            {
                switch (data.Type)
                {
                    case AssetType.Texture:
                    case AssetType.Sound:
                    case AssetType.ImageJPEG:
                    case AssetType.Mesh:
                        break;

                    default:
                        req.ErrorResponse(HttpStatusCode.NotFound);
                        return;
                }
            }

            string assetbase_header = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<AssetBase>";
            string flags = string.Empty;
            assetbase_header += (data.Data.Length != 0) ? "<Data>" : "<Data/>";

            if (0 != (data.Flags & AssetFlags.Maptile))
            {
                flags = "Maptile";
            }

            if (0 != (data.Flags & AssetFlags.Rewritable))
            {
                if (flags.Length != 0)
                {
                    flags += ",";
                }
                flags += "Rewritable";
            }

            if (0 != (data.Flags & AssetFlags.Collectable))
            {
                if (flags.Length != 0)
                {
                    flags += ",";
                }
                flags += "Collectable";
            }

            if (flags.Length == 0)
            {
                flags = "Normal";
            }
            string assetbase_footer = string.Empty;

            if (data.Data.Length != 0)
            {
                assetbase_footer = "</Data>" + assetbase_footer;
            }
            assetbase_footer += String.Format(
                    "<FullID><Guid>{0}</Guid></FullID><ID>{0}</ID><Name>{1}</Name><Description/><Type>{2}</Type><Local>{3}</Local><Temporary>{4}</Temporary><CreatorID>{5}</CreatorID><Flags>{6}</Flags></AssetBase>",
                    data.ID.ToString(),
                    data.Name.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"),
                    (int)data.Type,
                    data.Local.ToString(),
                    data.Temporary.ToString(),
                    data.Creator.ToString(),
                    flags);

            byte[] header = assetbase_header.ToUTF8Bytes();
            byte[] footer = assetbase_footer.ToUTF8Bytes();

            using (HttpResponse res = req.BeginResponse())
            {
                res.ContentType = "text/xml";
                using (Stream st = res.GetOutputStream())
                {
                    st.Write(header, 0, header.Length);
                    int pos = 0;
                    while (data.Data.Length - pos >= MAX_ASSET_BASE64_CONVERSION_SIZE)
                    {
                        string b = Convert.ToBase64String(data.Data, pos, MAX_ASSET_BASE64_CONVERSION_SIZE);
                        byte[] block = Encoding.UTF8.GetBytes(b);
                        st.Write(block, 0, block.Length);
                        pos += MAX_ASSET_BASE64_CONVERSION_SIZE;
                    }
                    if (data.Data.Length > pos)
                    {
                        string b = Convert.ToBase64String(data.Data, pos, data.Data.Length - pos);
                        byte[] block = Encoding.UTF8.GetBytes(b);
                        st.Write(block, 0, block.Length);
                    }
                    st.Write(footer, 0, footer.Length);
                }
            }
        }

        void GetAssetHandler_Metadata(HttpRequest req, UUID id)
        {
            AssetMetadata data;
            try
            {
                data = m_TemporaryAssetService.Metadata[id];
            }
            catch
            {
                try
                {
                    data = m_PersistentAssetService.Metadata[id];
                }
                catch
                {
                    try
                    {
                        data = m_ResourceAssetService.Metadata[id];
                    }
                    catch
                    {
                        req.ErrorResponse(HttpStatusCode.NotFound);
                        return;
                    }
                }
            }

            if (!m_EnableGet)
            {
                switch (data.Type)
                {
                    case AssetType.Texture:
                    case AssetType.Sound:
                    case AssetType.ImageJPEG:
                    case AssetType.Mesh:
                        break;

                    default:
                        req.ErrorResponse(HttpStatusCode.NotFound);
                        return;
                }
            }

            string assetbase_header = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<AssetMetadata>";
            string flags = string.Empty;

            if (0 != (data.Flags & AssetFlags.Maptile))
            {
                flags = "Maptile";
            }

            if (0 != (data.Flags & AssetFlags.Rewritable))
            {
                if (flags.Length != 0)
                {
                    flags += ",";
                }
                flags += "Rewritable";
            }

            if (0 != (data.Flags & AssetFlags.Collectable))
            {
                if (flags.Length != 0)
                {
                    flags += ",";
                }
                flags += "Collectable";
            }

            if (flags.Length == 0)
            {
                flags = "Normal";
            }
            string assetbase_footer = String.Format(
                "<FullID><Guid>{0}</Guid></FullID><ID>{0}</ID><Name>{1}</Name><Description/><Type>{2}</Type><Local>{3}</Local><Temporary>{4}</Temporary><CreatorID>{5}</CreatorID><Flags>{6}</Flags></AssetMetadata>",
                data.ID.ToString(),
                data.Name.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"),
                (int)data.Type,
                data.Local.ToString(),
                data.Temporary.ToString(),
                data.Creator.ToString(),
                flags);
            byte[] header = assetbase_header.ToUTF8Bytes();
            byte[] footer = assetbase_footer.ToUTF8Bytes();

            using (HttpResponse res = req.BeginResponse())
            {
                res.ContentType = "text/xml";
                using (Stream st = res.GetOutputStream(header.Length + footer.Length))
                {
                    st.Write(header, 0, header.Length);
                    st.Write(footer, 0, footer.Length);
                }
            }
        }

        void GetAssetHandler_Data(HttpRequest req, UUID id)
        {
            AssetData data;
            try
            {
                data = m_TemporaryAssetService[id];
            }
            catch
            {
                try
                {
                    data = m_PersistentAssetService[id];
                }
                catch
                {
                    try
                    {
                        data = m_ResourceAssetService[id];
                    }
                    catch
                    {
                        req.ErrorResponse(HttpStatusCode.NotFound);
                        return;
                    }
                }
            }

            if (!m_EnableGet)
            {
                switch (data.Type)
                {
                    case AssetType.Texture:
                    case AssetType.Sound:
                    case AssetType.ImageJPEG:
                    case AssetType.Mesh:
                        break;

                    default:
                        req.ErrorResponse(HttpStatusCode.NotFound);
                        return;
                }
            }

            using (HttpResponse res = req.BeginResponse())
            {
                res.ContentType = data.ContentType;
                bool compressionEnabled = true;
                switch (data.Type)
                {
                    case AssetType.Texture:
                    case AssetType.Sound:
                    case AssetType.ImageJPEG:
                        /* these are well-compressed no need for further compression */
                        compressionEnabled = false;
                        break;

                    default:
                        break;
                }
                using (Stream st = res.GetOutputStream(compressionEnabled))
                {
                    st.Write(data.Data, 0, data.Data.Length);
                }
            }
        }

        void DeleteAssetHandler(HttpRequest req)
        {
            using (HttpResponse res = req.BeginResponse())
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("boolean");
                    writer.WriteValue(false);
                    writer.WriteEndElement();
                }
            }
        }

        void PostAssetHandler(HttpRequest req)
        {
            AssetData data;
            try
            {
                data = AssetXml.ParseAssetData(req.Body);
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if ((data.Temporary || data.Local) && 
                null != m_TemporaryAssetService)
            {
                try
                {
                    m_TemporaryAssetService.Store(data);
                    using (HttpResponse res = req.BeginResponse())
                    {
                        using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                        {
                            writer.WriteStartElement("string");
                            writer.WriteValue(data.ID.ToString());
                            writer.WriteEndElement();
                        }
                    }
                }
                catch
                {
                    req.ErrorResponse(HttpStatusCode.InternalServerError);
                }
            }

            try
            {
                m_PersistentAssetService.Store(data);
                using (HttpResponse res = req.BeginResponse())
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("string");
                        writer.WriteValue(data.ID.ToString());
                        writer.WriteEndElement();
                    }
                }
            }
            catch(HttpResponse.ConnectionCloseException)
            {
                /* pass this one down to HttpServer */
                throw;
            }
            catch(Exception)
            {
                using (HttpResponse res = req.BeginResponse())
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("string");
                        writer.WriteValue(data.ID.ToString());
                        writer.WriteEndElement();
                    }
                }
            }
        }

        static UUID ParseUUID(XmlTextReader reader)
        {
            while (true)
            {
                if (!reader.Read())
                {
                    throw new InvalidDataException();
                }

                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name != "string")
                        {
                            throw new InvalidDataException();
                        }
                        break;

                    case XmlNodeType.Text:
                        return new UUID(reader.ReadContentAsString());

                    case XmlNodeType.EndElement:
                        throw new InvalidDataException();

                    default:
                        break;
                }
            }
        }

        static List<UUID> ParseArrayOfUUIDs(XmlTextReader reader)
        {
            List<UUID> result = new List<UUID>();
            bool haveroot = false;
            while (true)
            {
                if (!reader.Read())
                {
                    throw new InvalidDataException();
                }

                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (haveroot)
                        {
                            if (reader.Name != "string")
                            {
                                throw new InvalidDataException("Invalid ArrayOfString");
                            }
                            result.Add(ParseUUID(reader));
                        }
                        else
                        {
                            if(reader.Name != "ArrayOfString")
                            {
                                throw new InvalidDataException("Invalid ArrayOfString");
                            }
                            haveroot = true;
                        }
                        break;

                    case XmlNodeType.EndElement:
                        if (reader.Name != "ArrayOfString" || !haveroot)
                        {
                            throw new InvalidDataException("Invalid ArrayOfString");
                        }
                        return result;

                    default:
                        break;
                }
            }
        }

        void GetAssetsExistHandler(HttpRequest req)
        {
            if (req.Method != "POST")
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                return;
            }

            List<UUID> ids;
            try
            {
                using (XmlTextReader reader = new XmlTextReader(req.Body))
                {
                    ids = ParseArrayOfUUIDs(reader);
                }
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            Dictionary<UUID, bool> asset1;
            if (m_TemporaryAssetService != null)
            {
                asset1 = m_TemporaryAssetService.Exists(ids);
                foreach (KeyValuePair<UUID, bool> kvp in m_PersistentAssetService.Exists(ids))
                {
                    if (kvp.Value)
                    {
                        asset1[kvp.Key] = true;
                    }
                }
            }
            else
            {
                asset1 = m_PersistentAssetService.Exists(ids);
            }

            foreach (KeyValuePair<UUID, bool> kvp in m_ResourceAssetService.Exists(ids))
            {
                if (kvp.Value)
                {
                    asset1[kvp.Key] = true;
                }
            }

            using (HttpResponse res = req.BeginResponse())
            {
                res.ContentType = "text/xml";
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ArrayOfBoolean");
                    foreach (UUID id in ids)
                    {
                        bool found = false;
                        try
                        {
                            found = asset1[id];
                        }
                        catch
                        {
                            /* no action needed */
                        }
                        writer.WriteStartElement("boolean");
                        if (found)
                        {
                            writer.WriteValue("true");
                        }
                        else
                        {
                            writer.WriteValue("false");
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("AssetHandler")]
    public class RobustAssetHandlerFactory : IPluginFactory
    {
        public RobustAssetHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustAssetServerHandler(ownSection.GetString("PersistentAssetService", "AssetService"),
                ownSection.GetString("TemporaryAssetService", string.Empty), 
                ownSection.GetBoolean("IsGetEnabled", true),
                ownSection.GetBoolean("IsLimitedGetEnabled", false));
        }
    }
    #endregion
}
