// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types.StructuredData.AssetXml;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using System.Runtime.Serialization;

namespace SilverSim.BackendConnectors.Robust.Asset
{
    #region Service Implementation
    public class RobustAssetConnector : AssetServiceInterface, IPlugin
    {
        [Serializable]
        public class RobustAssetProtocolErrorException : Exception
        {
            public RobustAssetProtocolErrorException() { }
            public RobustAssetProtocolErrorException(string msg) : base(msg) {}
            public RobustAssetProtocolErrorException(string msg, Exception innerException) : base(msg, innerException) { }
            protected RobustAssetProtocolErrorException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        private static int MAX_ASSET_BASE64_CONVERSION_SIZE = 9 * 1024; /* must be an integral multiple of 3 */
        private int m_TimeoutMs = 20000;
        public int TimeoutMs
        {
            get
            {
                return m_TimeoutMs;
            }
            set
            {
                m_MetadataService.TimeoutMs = value;
                m_TimeoutMs = value;
            }
        }
        private string m_AssetURI;
        private RobustAssetMetadataConnector m_MetadataService;
        private DefaultAssetReferencesService m_ReferencesService;
        private RobustAssetDataConnector m_DataService;
        private bool m_EnableCompression;
        private bool m_EnableLocalStorage;
        private bool m_EnableTempStorage;

        #region Constructor
        public RobustAssetConnector(string uri, bool enableCompression = false, bool enableLocalStorage = false, bool enableTempStorage = false)
        {
            if(!uri.EndsWith("/"))
            {
                uri += "/";
            }

            m_AssetURI = uri;
            m_DataService = new RobustAssetDataConnector(uri);
            m_MetadataService = new RobustAssetMetadataConnector(uri);
            m_ReferencesService = new DefaultAssetReferencesService(this);
            m_MetadataService.TimeoutMs = m_TimeoutMs;
            m_EnableCompression = enableCompression;
            m_EnableLocalStorage = enableLocalStorage;
            m_EnableTempStorage = enableTempStorage;
        }

        public void Startup(ConfigurationLoader loader)
        {

        }
        #endregion

        #region Exists methods
        public override bool Exists(UUID key)
        {
            try
            {
                HttpRequestHandler.DoGetRequest(m_AssetURI + "assets/" + key.ToString() + "/metadata", null, TimeoutMs);
            }
            catch(HttpException e)
            {
                if (e.GetHttpCode() == (int)HttpStatusCode.NotFound)
                {
                    return false;
                }
                throw;
            }
            return true;
        }

        static bool ParseBoolean(XmlTextReader reader)
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
                        if (reader.Name != "boolean")
                        {
                            throw new InvalidDataException();
                        }
                        break;

                    case XmlNodeType.Text:
                        return reader.ReadContentAsBoolean();

                    case XmlNodeType.EndElement:
                        throw new InvalidDataException();
                }
            }
        }

        static List<bool> ParseArrayOfBoolean(XmlTextReader reader)
        {
            List<bool> result = new List<bool>();
            while(true)
            {
                if(!reader.Read())
                {
                    throw new InvalidDataException();
                }

                switch(reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if(reader.Name != "boolean")
                        {
                            throw new InvalidDataException();
                        }
                        result.Add(ParseBoolean(reader));
                        break;

                    case XmlNodeType.EndElement:
                        if(reader.Name != "ArrayOfBoolean")
                        {
                            throw new InvalidDataException();
                        }
                        return result;
                }
            }
        }

        static List<bool> ParseAssetsExistResponse(XmlTextReader reader)
        {
            while(true)
            {
                if(!reader.Read())
                {
                    throw new InvalidDataException();
                }

                if(reader.NodeType == XmlNodeType.Element)
                {
                    if(reader.Name != "ArrayOfBoolean")
                    {
                        throw new InvalidDataException();
                    }

                    return ParseArrayOfBoolean(reader);
                }
            }
        }

        public override Dictionary<UUID, bool> Exists(List<UUID> assets)
        {
            Dictionary<UUID, bool> res = new Dictionary<UUID,bool>();
            string xmlreq = "<?xml version=\"1.0\"?>";
            xmlreq += "<ArrayOfString xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">";
            foreach(UUID asset in assets)
            {
                xmlreq += "<string>" + asset.ToString() + "</string>";
            }
            xmlreq += "</ArrayOfString>";

            
            try
            {
                using (Stream xmlres = HttpRequestHandler.DoStreamRequest("POST", m_AssetURI + "get_assets_exist", null, "text/xml", xmlreq, false, TimeoutMs))
                {
                    try
                    {
                        using (XmlTextReader xmlreader = new XmlTextReader(xmlres))
                        {
                            List<bool> response = ParseAssetsExistResponse(xmlreader);
                            if (response.Count != assets.Count)
                            {
                                throw new RobustAssetProtocolErrorException("Invalid response for get_assets_exist received");
                            }
                            for (int i = 0; i < assets.Count; ++i)
                            {
                                res.Add(assets[i], response[i]);
                            }
                        }
                    }
                    catch
                    {
                        throw new RobustAssetProtocolErrorException("Invalid response for get_assets_exist received");
                    }
                }
            }
            catch
            {
                foreach(UUID asset in assets)
                {
                    res[asset] = false;
                }
                return res;
            }
            return res;
        }
        #endregion

        #region Accessors
        public override AssetData this[UUID key]
        {
            get
            {
                try
                {
                    using (Stream stream = HttpRequestHandler.DoStreamGetRequest(m_AssetURI + "assets/" + key.ToString(), null, TimeoutMs))
                    {
                        return AssetXml.ParseAssetData(stream);
                    }
                }
                catch(HttpException e)
                {
                    if (e.GetHttpCode() == (int)HttpStatusCode.NotFound)
                    {
                        throw new AssetNotFoundException(key);
                    }
                    throw;
                }
            }
        }
        #endregion

        #region Metadata interface
        public override AssetMetadataServiceInterface Metadata
        {
            get
            {
                return m_MetadataService;
            }
        }
        #endregion

        #region References interface
        public override AssetReferencesServiceInterface References
        {
            get
            {
                return m_ReferencesService;
            }
        }
        #endregion

        #region Data interface
        public override AssetDataServiceInterface Data
        {
            get
            {
                return m_DataService;
            }
        }
        #endregion

        #region Store asset method
        static Encoding UTF8NoBOM = new UTF8Encoding(false);

        public override void Store(AssetData asset)
        {
            if((asset.Temporary && !m_EnableTempStorage) ||
                (asset.Local && !m_EnableLocalStorage))
            {
                /* Do not store temporary or local assets on specified server unless explicitly wanted */
                return;
            }
            string assetbase_header = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<AssetBase>";
            string flags = string.Empty;
            if(asset.Data.Length != 0)
            {
                assetbase_header += "<Data>";
            }
            else
            {
                assetbase_header += "<Data/>";
            }

            if(0 != (asset.Flags & AssetFlags.Maptile))
            {
                flags = "Maptile";
            }

            if (0 != (asset.Flags & AssetFlags.Rewritable))
            {
                if(flags.Length != 0)
                {
                    flags += ",";
                }
                flags += "Rewritable";
            }

            if (0 != (asset.Flags & AssetFlags.Collectable))
            {
                if (flags.Length != 0)
                {
                    flags += ",";
                }
                flags += "Collectable";
            }

            if(flags.Length == 0)
            {
                flags = "Normal";
            }
            string assetbase_footer = string.Empty;

            if (asset.Data.Length != 0)
            {
                assetbase_footer += "</Data>";
            }
            assetbase_footer += String.Format(
                "<FullID><Guid>{0}</Guid></FullID><ID>{0}</ID><Name>{1}</Name><Description/><Type>{2}</Type><Local>{3}</Local><Temporary>{4}</Temporary><CreatorID>{5}</CreatorID><Flags>{6}</Flags></AssetBase>",
                asset.ID.ToString(),
                asset.Name.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"),
                (int)asset.Type,
                asset.Local.ToString(),
                asset.Temporary.ToString(),
                asset.Creator.ToString(),
                flags);

            byte[] header = UTF8NoBOM.GetBytes(assetbase_header);
            byte[] footer = UTF8NoBOM.GetBytes(assetbase_footer);
            if (m_EnableCompression)
            {
                byte[] compressedAsset;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress))
                    {
                        gz.Write(header, 0, header.Length);
                        WriteAssetDataAsBase64(gz, asset);
                        gz.Write(footer, 0, footer.Length);
                    }
                    compressedAsset = ms.GetBuffer();
                }
                HttpRequestHandler.DoRequest("POST", m_AssetURI + "assets",
                    null, "text/xml", compressedAsset.Length, delegate(Stream st)
                    {
                        /* Stream based asset conversion method here */
                        st.Write(compressedAsset, 0, compressedAsset.Length);
                    }, m_EnableCompression, TimeoutMs);
            }
            else
            {
                int base64_codegroups = (asset.Data.Length + 2) / 3;
                HttpRequestHandler.DoRequest("POST", m_AssetURI + "assets",
                    null, "text/xml", 4 * base64_codegroups + header.Length + footer.Length, delegate(Stream st)
                {
                    /* Stream based asset conversion method here */
                    st.Write(header, 0, header.Length);
                    WriteAssetDataAsBase64(st, asset);
                    st.Write(footer, 0, footer.Length);
                }, m_EnableCompression, TimeoutMs);
            }
        }

        void WriteAssetDataAsBase64(Stream st, AssetData asset)
        {
            int pos = 0;
            while (asset.Data.Length - pos >= MAX_ASSET_BASE64_CONVERSION_SIZE)
            {
                string b = Convert.ToBase64String(asset.Data, pos, MAX_ASSET_BASE64_CONVERSION_SIZE);
                byte[] block = UTF8NoBOM.GetBytes(b);
                st.Write(block, 0, block.Length);
                pos += MAX_ASSET_BASE64_CONVERSION_SIZE;
            }
            if (asset.Data.Length > pos)
            {
                string b = Convert.ToBase64String(asset.Data, pos, asset.Data.Length - pos);
                byte[] block = UTF8NoBOM.GetBytes(b);
                st.Write(block, 0, block.Length);
            }
        }
        #endregion

        #region Delete asset method
        public override void Delete(UUID id)
        {
            try
            {
                HttpRequestHandler.DoRequest("DELETE", m_AssetURI + "/" + id.ToString(), null, string.Empty, null, false, TimeoutMs);
            }
            catch
            {
                throw new AssetNotFoundException(id);
            }
        }
        #endregion
    }
    #endregion

    #region Factory
    [PluginName("Assets")]
    public class RobustAssetConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST ASSET CONNECTOR");
        public RobustAssetConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustAssetConnector(
                ownSection.GetString("URI"), 
                ownSection.GetBoolean("EnableCompressedStoreRequest", false),
                ownSection.GetBoolean("EnableLocalAssetStorage", false),
                ownSection.GetBoolean("EnableTempAssetStorage", false));
        }
    }
    #endregion
}
