// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Http.Client;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types.StructuredData.AssetXml;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System.IO;
using System.Web;

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
                try
                {
                    using(Stream stream = HttpRequestHandler.DoStreamGetRequest(m_AssetURI + "assets/" + key.ToString() + "/metadata", null, TimeoutMs))
                    {
                        return AssetXml.ParseAssetMetadata(stream);
                    }
                }
                catch(HttpException)
                {
                    throw new AssetNotFoundException(key);
                }
            }
        }
        #endregion
    }
}
