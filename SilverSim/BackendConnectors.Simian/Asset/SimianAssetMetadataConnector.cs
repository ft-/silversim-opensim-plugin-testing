// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Simian.Common;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Simian.Asset
{
    public sealed class SimianAssetMetadataConnector : AssetMetadataServiceInterface
    {
        public int TimeoutMs = 20000;
        readonly string m_AssetURI;
        readonly string m_AssetCapability;

        #region Constructor
        public SimianAssetMetadataConnector(string uri, string capability)
        {
            m_AssetURI = uri;
            m_AssetCapability = capability;
        }
        #endregion

        #region Metadata accessors
        public override bool TryGetValue(UUID key, out AssetMetadata metadata)
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

        public override AssetMetadata this[UUID key]
        {
            get
            {
                AssetMetadata metadata;
                if(!TryGetValue(key, out metadata))
                {
                    throw new AssetNotFoundException(key);
                }
                return metadata;
            }
        }
        #endregion
    }
}
