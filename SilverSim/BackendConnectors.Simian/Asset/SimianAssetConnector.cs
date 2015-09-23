// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Simian.Common;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System;
using System.Collections.Generic;
using System.Text;

namespace SilverSim.BackendConnectors.Simian.Asset
{
    #region Service Implementation
    public class SimianAssetConnector : AssetServiceInterface, IPlugin
    {
        public class SimianAssetProtocolError : Exception
        {
            public SimianAssetProtocolError(string msg) : base(msg) {}
        }

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
        private SimianAssetMetadataConnector m_MetadataService;
        private DefaultAssetReferencesService m_ReferencesService;
        private SimianAssetDataConnector m_DataService;
        private bool m_EnableCompression = false;
        private bool m_EnableLocalStorage = false;
        private bool m_EnableTempStorage = false;
        private string m_AssetCapability = "00000000-0000-0000-0000-000000000000";

        #region Constructor
        public SimianAssetConnector(string uri, string capability, bool enableCompression = false, bool enableLocalStorage = false, bool enableTempStorage = false)
        {
            m_AssetCapability = capability;
            if(!uri.EndsWith("/") && !uri.EndsWith("="))
            {
                uri += "/";
            }

            m_AssetURI = uri;
            m_DataService = new SimianAssetDataConnector(uri, m_AssetCapability);
            m_MetadataService = new SimianAssetMetadataConnector(uri, m_AssetCapability);
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
        public override void exists(UUID key)
        {
            /* using the metadata variant is always faster no need for transfering data */
            AssetMetadata m = m_MetadataService[key];
        }

        public override Dictionary<UUID, bool> exists(List<UUID> assets)
        {
            Dictionary<UUID, bool> res = new Dictionary<UUID,bool>();

            foreach(UUID assetid in assets)
            {
                try
                {
                    exists(assetid);
                    res[assetid] = true;
                }
                catch(AssetNotFound)
                {
                    res[assetid] = false;
                }
            }

            return res;
        }
        #endregion

        #region Accessors
        public override AssetData this[UUID key]
        {
            get
            {
                Dictionary<string, string> para = new Dictionary<string, string>();
                para["RequestMethod"] = "xGetAsset";
                para["ID"] = (string)key;
                Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, TimeoutMs);
                if (!m["Success"].AsBoolean)
                {
                    throw new AssetNotFound(key);
                }
                AssetData data = new AssetData();
                data.ID = key;
                if (m.ContainsKey("Name"))
                {
                    data.Name = m["Name"].ToString();
                }
                else
                {
                    data.Name = string.Empty;
                }
                data.ContentType = m["ContentType"].ToString();
                data.Creator.FullName = m["CreatorID"].ToString();
                data.Local = false;
                data.Data = Convert.FromBase64String(m["EncodedData"].ToString());
                data.Temporary = m["Temporary"].AsBoolean;
                return data;
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

            Dictionary<string, string> para = new Dictionary<string, string>();
            para["RequestMethod"] = "xAddAsset";
            para["ContentType"] = asset.ContentType;
            para["EncodedData"] = Convert.ToBase64String(asset.Data);
            para["AssetID"] = (string)asset.ID;
            para["CreatorID"] = asset.Creator.FullName;
            para["Temporary"] = asset.Temporary ? "1" : "0";
            para["Name"] = asset.Name;

            Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, m_EnableCompression, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                throw new AssetStoreFailed(asset.ID);
            }
        }
        #endregion

        #region Delete asset method
        public override void Delete(UUID id)
        {
            Dictionary<string, string> para = new Dictionary<string, string>();
            para["RequestMethod"] = "xRemoveAsset";
            para["AssetID"] = (string)id;

            Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                throw new AssetNotFound(id);
            }
        }
        #endregion
    }
    #endregion

    #region Factory
    [PluginName("Assets")]
    public class SimianAssetConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("SIMIAN ASSET CONNECTOR");
        public SimianAssetConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new SimianAssetConnector(
                ownSection.GetString("URI"),
                ownSection.GetString("SimCapability", "00000000-0000-0000-0000-000000000000"),
                ownSection.GetBoolean("EnableCompressedStoreRequest", false),
                ownSection.GetBoolean("EnableLocalAssetStorage", false),
                ownSection.GetBoolean("EnableTempAssetStorage", false));
        }
    }
    #endregion
}
