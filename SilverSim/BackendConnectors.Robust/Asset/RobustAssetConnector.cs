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
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.StructuredData.AssetXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Web;
using System.Xml;

namespace SilverSim.BackendConnectors.Robust.Asset
{
    [Description("Robust Asset Connector")]
    [PluginName("Assets")]
    public class RobustAssetConnector : AssetServiceInterface, IPlugin, IAssetMetadataServiceInterface, IAssetDataServiceInterface
    {
        [Serializable]
        public class RobustAssetProtocolErrorException : Exception
        {
            public RobustAssetProtocolErrorException()
            {
            }

            public RobustAssetProtocolErrorException(string msg) : base(msg)
            {
            }

            public RobustAssetProtocolErrorException(string msg, Exception innerException) : base(msg, innerException)
            {
            }

            protected RobustAssetProtocolErrorException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST ASSET CONNECTOR");

        private const int MAX_ASSET_BASE64_CONVERSION_SIZE = 9 * 1024; /* must be an integral multiple of 3 */
        public int TimeoutMs { get; set; }
        private readonly string m_AssetURI;
        private readonly DefaultAssetReferencesService m_ReferencesService;
        private readonly bool m_EnableCompression;
        private readonly bool m_EnableLocalStorage;
        private readonly bool m_EnableTempStorage;

        #region Constructor
        public RobustAssetConnector(string uri, bool enableCompression = false, bool enableLocalStorage = false, bool enableTempStorage = false)
        {
            TimeoutMs = 20000;
            if(!uri.EndsWith("/"))
            {
                uri += "/";
            }

            m_AssetURI = uri;
            m_ReferencesService = new DefaultAssetReferencesService(this);
            m_EnableCompression = enableCompression;
            m_EnableLocalStorage = enableLocalStorage;
            m_EnableTempStorage = enableTempStorage;
        }

        public RobustAssetConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            string uri = ownSection.GetString("URI");

            TimeoutMs = 20000;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }

            m_AssetURI = uri;
            m_ReferencesService = new DefaultAssetReferencesService(this);
            m_EnableCompression = ownSection.GetBoolean("EnableCompressedStoreRequest", false);
            m_EnableLocalStorage = ownSection.GetBoolean("EnableLocalAssetStorage", false);
            m_EnableTempStorage = ownSection.GetBoolean("EnableTempAssetStorage", false);
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        public override bool IsSameServer(AssetServiceInterface other)
        {
            return other.GetType() == typeof(RobustAssetConnector) &&
                (m_AssetURI == ((RobustAssetConnector)other).m_AssetURI);
        }

        #region Exists methods
        public override bool Exists(UUID key)
        {
            try
            {
                new HttpClient.Get(m_AssetURI + "assets/" + key.ToString() + "/metadata")
                {
                    TimeoutMs = TimeoutMs
                }.ExecuteRequest();
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

        private static bool ParseBoolean(XmlTextReader reader)
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

                    default:
                        break;
                }
            }
        }

        private static List<bool> ParseArrayOfBoolean(XmlTextReader reader)
        {
            var result = new List<bool>();
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

                    default:
                        break;
                }
            }
        }

        private static List<bool> ParseAssetsExistResponse(XmlTextReader reader)
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
            var res = new Dictionary<UUID,bool>();
            var xmlreq = new StringBuilder("<?xml version=\"1.0\"?>");
            xmlreq.Append("<ArrayOfString>");
            foreach(UUID asset in assets)
            {
                xmlreq.Append("<string>");
                xmlreq.Append(asset.ToString());
                xmlreq.Append("</string>");
            }
            xmlreq.Append("</ArrayOfString>");

            try
            {
                using (Stream xmlres = new HttpClient.Post(m_AssetURI + "get_assets_exist", "text/xml", xmlreq.ToString())
                {
                    TimeoutMs = TimeoutMs
                }.ExecuteStreamRequest())
                {
                    try
                    {
                        using (XmlTextReader xmlreader = xmlres.CreateXmlReader())
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
        public override bool TryGetValue(UUID key, out AssetData assetData)
        {
            try
            {
                using (Stream stream = new HttpClient.Get(m_AssetURI + "assets/" + key.ToString())
                {
                    TimeoutMs = TimeoutMs
                }.ExecuteStreamRequest())
                {
                    assetData = AssetXml.ParseAssetData(stream);
                    return true;
                }
            }
            catch (HttpException e)
            {
                if (e.GetHttpCode() == (int)HttpStatusCode.NotFound)
                {
                    assetData = default(AssetData);
                    return false;
                }
                throw;
            }
        }

        public override AssetData this[UUID key]
        {
            get
            {
                AssetData assetData;
                if(!TryGetValue(key, out assetData))
                {
                    throw new AssetNotFoundException(key);
                }
                return assetData;
            }
        }
        #endregion

        #region Metadata interface
        public override IAssetMetadataServiceInterface Metadata => this;

        bool IAssetMetadataServiceInterface.TryGetValue(UUID key, out AssetMetadata metadata)
        {
            try
            {
                using (Stream stream = new HttpClient.Get(m_AssetURI + "assets/" + key.ToString() + "/metadata")
                {
                    TimeoutMs = TimeoutMs
                }.ExecuteStreamRequest())
                {
                    metadata = AssetXml.ParseAssetMetadata(stream);
                    return true;
                }
            }
            catch (HttpException e)
            {
                if (e.GetHttpCode() == (int)HttpStatusCode.NotFound)
                {
                    metadata = default(AssetData);
                    return false;
                }
                throw;
            }
        }

        AssetMetadata IAssetMetadataServiceInterface.this[UUID key]
        {
            get
            {
                AssetMetadata metadata;
                if (!Metadata.TryGetValue(key, out metadata))
                {
                    throw new AssetNotFoundException(key);
                }
                return metadata;
            }
        }
        #endregion

        #region References interface
        public override AssetReferencesServiceInterface References => m_ReferencesService;
        #endregion

        #region Data interface
        public override IAssetDataServiceInterface Data => this;

        bool IAssetDataServiceInterface.TryGetValue(UUID key, out Stream s)
        {
            try
            {
                s = new HttpClient.Get(m_AssetURI + "assets/" + key.ToString() + "/data")
                {
                    TimeoutMs = TimeoutMs
                }.ExecuteStreamRequest();
                return true;
            }
            catch
            {
                s = null;
                return false;
            }
        }

        Stream IAssetDataServiceInterface.this[UUID key]
        {
            get
            {
                Stream s;
                if (!Data.TryGetValue(key, out s))
                {
                    throw new AssetNotFoundException(key);
                }
                return s;
            }
        }
        #endregion

        #region Store asset method

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
            assetbase_header += (asset.Data.Length != 0) ? "<Data>" : "<Data/>";

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
                UUID.Zero,
                flags);

            byte[] header = assetbase_header.ToUTF8Bytes();
            byte[] footer = assetbase_footer.ToUTF8Bytes();
            if (m_EnableCompression)
            {
                byte[] compressedAsset;
                using (var ms = new MemoryStream())
                {
                    using (var gz = new GZipStream(ms, CompressionMode.Compress))
                    {
                        gz.Write(header, 0, header.Length);
                        WriteAssetDataAsBase64(gz, asset);
                        gz.Write(footer, 0, footer.Length);
                    }
                    compressedAsset = ms.ToArray();
                }
                new HttpClient.Post(m_AssetURI + "assets",
                    "text/xml", compressedAsset.Length, (Stream st) =>
                    {
                        /* Stream based asset conversion method here */
                        st.Write(compressedAsset, 0, compressedAsset.Length);
                    })
                {
                    IsCompressed = m_EnableCompression,
                    TimeoutMs = TimeoutMs
                }.ExecuteRequest();
            }
            else
            {
                int base64_codegroups = (asset.Data.Length + 2) / 3;
                new HttpClient.Post(m_AssetURI + "assets",
                    "text/xml", 4 * base64_codegroups + header.Length + footer.Length, (Stream st) =>
                    {
                        /* Stream based asset conversion method here */
                        st.Write(header, 0, header.Length);
                        WriteAssetDataAsBase64(st, asset);
                        st.Write(footer, 0, footer.Length);
                    })
                {
                    IsCompressed = m_EnableCompression,
                    TimeoutMs = TimeoutMs
                }.ExecuteRequest();
            }
        }

        private void WriteAssetDataAsBase64(Stream st, AssetData asset)
        {
            int pos = 0;
            while (asset.Data.Length - pos >= MAX_ASSET_BASE64_CONVERSION_SIZE)
            {
                string b = Convert.ToBase64String(asset.Data, pos, MAX_ASSET_BASE64_CONVERSION_SIZE);
                byte[] block = b.ToUTF8Bytes();
                st.Write(block, 0, block.Length);
                pos += MAX_ASSET_BASE64_CONVERSION_SIZE;
            }
            if (asset.Data.Length > pos)
            {
                string b = Convert.ToBase64String(asset.Data, pos, asset.Data.Length - pos);
                byte[] block = b.ToUTF8Bytes();
                st.Write(block, 0, block.Length);
            }
        }
        #endregion

        #region Delete asset method
        public override void Delete(UUID id)
        {
            try
            {
                new HttpClient.Delete(m_AssetURI + "/" + id.ToString())
                {
                    TimeoutMs = TimeoutMs
                }.ExecuteRequest();
            }
            catch
            {
                throw new AssetNotFoundException(id);
            }
        }
        #endregion
    }
}
