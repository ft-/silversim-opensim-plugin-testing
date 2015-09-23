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
    public class SimianAssetMetadataConnector : AssetMetadataServiceInterface
    {
        public int TimeoutMs = 20000;
        private string m_AssetURI;
        private string m_AssetCapability;

        #region Constructor
        public SimianAssetMetadataConnector(string uri, string capability)
        {
            m_AssetURI = uri;
            m_AssetCapability = capability;
        }
        #endregion

        #region Metadata accessors
        public override AssetMetadata this[UUID key]
        {
            get
            {
                Dictionary<string, string> para = new Dictionary<string, string>();
                para["RequestMethod"] = "xGetAssetMetadata";
                para["ID"] = (string)key;
                Map m = SimianGrid.PostToService(m_AssetURI, m_AssetCapability, para, TimeoutMs);
                if(!m["Success"].AsBoolean)
                {
                    throw new AssetNotFound(key);
                }
                AssetMetadata data = new AssetMetadata();
                data.ID = key;
                data.Name = string.Empty;
                data.ContentType = m["ContentType"].ToString();
                data.Creator.FullName = m["CreatorID"].ToString();
                data.Local = false;
                data.Temporary = m["Temporary"].AsBoolean;

                string lastModifiedStr = m["Last-Modified"].ToString();
                if (!string.IsNullOrEmpty(lastModifiedStr))
                {
                    DateTime lastModified;
                    if (DateTime.TryParse(lastModifiedStr, out lastModified))
                    {
                        data.CreateTime = new Date(lastModified);
                    }
                }
                return data;
            }
        }
        #endregion
    }
}
