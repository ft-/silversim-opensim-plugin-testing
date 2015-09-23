// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.StructuredData.AssetXml;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Asset
{
    public class RobustAssetMetadataConnector : AssetMetadataServiceInterface
    {
        public int TimeoutMs = 20000;
        private string m_AssetURI;

        #region Constructor
        public RobustAssetMetadataConnector(string uri)
        {
            m_AssetURI = uri;
        }
        #endregion

        #region Metadata accessors
        public override AssetMetadata this[UUID key]
        {
            get
            {
                Stream stream;
                try
                {
                    stream = HttpRequestHandler.DoStreamGetRequest(m_AssetURI + "assets/" + key.ToString() + "/metadata", null, TimeoutMs);
                }
                catch
                {
                    throw new AssetNotFound(key);
                }
                return AssetXml.parseAssetMetadata(stream);
            }
        }
        #endregion
    }
}
