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
using SilverSim.BackendConnectors.Simian.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace SilverSim.BackendConnectors.Simian.Asset
{
    [Description("Simian Asset Connector")]
    [PluginName("Assets")]
    public sealed class SimianAssetConnector : AssetServiceInterface, IPlugin, IAssetMetadataServiceInterface, IAssetDataServiceInterface
    {
        [Serializable]
        public class SimianAssetProtocolErrorException : Exception
        {
            public SimianAssetProtocolErrorException()
            {
            }

            public SimianAssetProtocolErrorException(string msg) : base(msg)
            {
            }

            public SimianAssetProtocolErrorException(string msg, Exception innerException) : base(msg, innerException)
            {
            }

            protected SimianAssetProtocolErrorException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

        private static readonly ILog m_Log = LogManager.GetLogger("SIMIAN ASSET CONNECTOR");

        public int TimeoutMs { get; set; }
        private readonly string m_AssetURI;
        private readonly DefaultAssetReferencesService m_ReferencesService;
        private readonly bool m_EnableCompression;
        private readonly bool m_EnableLocalStorage;
        private readonly bool m_EnableTempStorage;
        private readonly string m_AssetCapability = "00000000-0000-0000-0000-000000000000";

        #region Constructor
        public SimianAssetConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            string uri = ownSection.GetString("URI");
            m_AssetCapability = ownSection.GetString("SimCapability", "00000000-0000-0000-0000-000000000000");
            m_EnableCompression = ownSection.GetBoolean("EnableCompressedStoreRequest", false);
            m_EnableLocalStorage = ownSection.GetBoolean("EnableLocalAssetStorage", false);
            m_EnableTempStorage = ownSection.GetBoolean("EnableTempAssetStorage", false);

            TimeoutMs = 20000;
            if (!uri.EndsWith("/") && !uri.EndsWith("="))
            {
                uri += "/";
            }

            m_AssetURI = uri;
            m_ReferencesService = new DefaultAssetReferencesService(this);
        }

        public SimianAssetConnector(string uri, string capability, bool enableCompression = false, bool enableLocalStorage = false, bool enableTempStorage = false)
        {
            TimeoutMs = 20000;
            m_AssetCapability = capability;
            if(!uri.EndsWith("/") && !uri.EndsWith("="))
            {
                uri += "/";
            }

            m_AssetURI = uri;
            m_ReferencesService = new DefaultAssetReferencesService(this);
            m_EnableCompression = enableCompression;
            m_EnableLocalStorage = enableLocalStorage;
            m_EnableTempStorage = enableTempStorage;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        #region Exists methods
        public override bool Exists(UUID key)
        {
            /* using the metadata variant is always faster no need for transfering data */
            try
            {
                var para = new Dictionary<string, string>
                {
                    ["RequestMethod"] = "xGetAssetMetadata",
                    ["ID"] = (string)key
                };
                Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, TimeoutMs);
                return m["Success"].AsBoolean;
            }
            catch
            {
                return false;
            }
        }

        public override Dictionary<UUID, bool> Exists(List<UUID> assets)
        {
            var res = new Dictionary<UUID,bool>();

            foreach(UUID assetid in assets)
            {
                res[assetid] = Exists(assetid);
            }

            return res;
        }
        #endregion

        #region Accessors
        public override bool TryGetValue(UUID key, out AssetData assetData)
        {
            var para = new Dictionary<string, string>
            {
                ["RequestMethod"] = "xGetAsset",
                ["ID"] = (string)key
            };
            Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, TimeoutMs);
            if(!m.ContainsKey("Success"))
            {
                assetData = default(AssetData);
                return false;
            }
            if (!m["Success"].AsBoolean)
            {
                assetData = default(AssetData);
                return false;
            }
            assetData = new AssetData()
            {
                ID = key,
                Name = m.ContainsKey("Name") ? m["Name"].ToString() : string.Empty,
                ContentType = m["ContentType"].ToString(),
                Local = false,
                Data = Convert.FromBase64String(m["EncodedData"].ToString()),
                Temporary = m["Temporary"].AsBoolean
            };
            assetData.Creator.FullName = m["CreatorID"].ToString();
            return true;
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
            var para = new Dictionary<string, string>
            {
                ["RequestMethod"] = "xGetAssetMetadata",
                ["ID"] = (string)key
            };
            Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                metadata = default(AssetMetadata);
                return false;
            }
            metadata = new AssetMetadata()
            {
                ID = key,
                Name = string.Empty,
                ContentType = m["ContentType"].ToString(),
                Local = false,
                Temporary = m["Temporary"].AsBoolean
            };
            metadata.Creator.FullName = m["CreatorID"].ToString();

            string lastModifiedStr = m["Last-Modified"].ToString();
            if (lastModifiedStr?.Length != 0)
            {
                DateTime lastModified;
                if (DateTime.TryParse(lastModifiedStr, out lastModified))
                {
                    metadata.CreateTime = new Date(lastModified);
                }
            }
            return true;
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
                s = HttpClient.DoStreamGetRequest(m_AssetURI + "assets/" + key.ToString() + "/data", null, TimeoutMs);
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
                if (Data.TryGetValue(key, out s))
                {
                    return s;
                }
                else
                {
                    throw new AssetNotFoundException(key);
                }
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

            var para = new Dictionary<string, string>
            {
                ["RequestMethod"] = "xAddAsset",
                ["ContentType"] = asset.ContentType,
                ["EncodedData"] = Convert.ToBase64String(asset.Data),
                ["AssetID"] = (string)asset.ID,
                ["CreatorID"] = asset.Creator.FullName,
                ["Temporary"] = asset.Temporary ? "1" : "0",
                ["Name"] = asset.Name
            };
            Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, m_EnableCompression, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                throw new AssetStoreFailedException(asset.ID);
            }
        }
        #endregion

        #region Delete asset method
        public override void Delete(UUID id)
        {
            var para = new Dictionary<string, string>
            {
                ["RequestMethod"] = "xRemoveAsset",
                ["AssetID"] = (string)id
            };
            Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                throw new AssetNotFoundException(id);
            }
        }
        #endregion
    }
}
