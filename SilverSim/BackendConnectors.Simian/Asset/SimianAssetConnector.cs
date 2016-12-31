﻿// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace SilverSim.BackendConnectors.Simian.Asset
{
    #region Service Implementation
    [Description("Simian Asset Connector")]
    public sealed class SimianAssetConnector : AssetServiceInterface, IPlugin, IAssetMetadataServiceInterface, IAssetDataServiceInterface
    {
        [Serializable]
        public class SimianAssetProtocolErrorException : Exception
        {
            public SimianAssetProtocolErrorException() { }
            public SimianAssetProtocolErrorException(string msg) : base(msg) {}
            public SimianAssetProtocolErrorException(string msg, Exception innerException) : base(msg, innerException) { }
            protected SimianAssetProtocolErrorException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        public int TimeoutMs { get; set; }
        readonly string m_AssetURI;
        readonly DefaultAssetReferencesService m_ReferencesService;
        readonly bool m_EnableCompression;
        readonly bool m_EnableLocalStorage;
        readonly bool m_EnableTempStorage;
        readonly string m_AssetCapability = "00000000-0000-0000-0000-000000000000";

        #region Constructor
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
                Dictionary<string, string> para = new Dictionary<string, string>();
                para["RequestMethod"] = "xGetAssetMetadata";
                para["ID"] = (string)key;
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
            Dictionary<UUID, bool> res = new Dictionary<UUID,bool>();

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
            Dictionary<string, string> para = new Dictionary<string, string>();
            para["RequestMethod"] = "xGetAsset";
            para["ID"] = (string)key;
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
            assetData = new AssetData();
            assetData.ID = key;
            assetData.Name = m.ContainsKey("Name") ? m["Name"].ToString() : string.Empty;
            assetData.ContentType = m["ContentType"].ToString();
            assetData.Creator.FullName = m["CreatorID"].ToString();
            assetData.Local = false;
            assetData.Data = Convert.FromBase64String(m["EncodedData"].ToString());
            assetData.Temporary = m["Temporary"].AsBoolean;
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
        public override IAssetMetadataServiceInterface Metadata
        {
            get
            {
                return this;
            }
        }

        bool IAssetMetadataServiceInterface.TryGetValue(UUID key, out AssetMetadata metadata)
        {
            Dictionary<string, string> para = new Dictionary<string, string>();
            para["RequestMethod"] = "xGetAssetMetadata";
            para["ID"] = (string)key;
            Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, TimeoutMs);
            if (!m["Success"].AsBoolean)
            {
                metadata = default(AssetMetadata);
                return false;
            }
            metadata = new AssetMetadata();
            metadata.ID = key;
            metadata.Name = string.Empty;
            metadata.ContentType = m["ContentType"].ToString();
            metadata.Creator.FullName = m["CreatorID"].ToString();
            metadata.Local = false;
            metadata.Temporary = m["Temporary"].AsBoolean;

            string lastModifiedStr = m["Last-Modified"].ToString();
            if (!string.IsNullOrEmpty(lastModifiedStr))
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
        public override AssetReferencesServiceInterface References
        {
            get
            {
                return m_ReferencesService;
            }
        }
        #endregion

        #region Data interface
        public override IAssetDataServiceInterface Data
        {
            get
            {
                return this;
            }
        }

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

        [SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule")]
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
                throw new AssetStoreFailedException(asset.ID);
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
                throw new AssetNotFoundException(id);
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
                throw new ConfigurationLoader.ConfigurationErrorException();
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
