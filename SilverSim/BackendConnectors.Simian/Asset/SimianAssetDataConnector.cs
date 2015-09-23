// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using System.IO;

namespace SilverSim.BackendConnectors.Simian.Asset
{
    public class SimianAssetDataConnector : AssetDataServiceInterface
    {
        public int TimeoutMs = 20000;
        private string m_AssetURI;
        private string m_AssetCapability;

        #region Constructor
        public SimianAssetDataConnector(string uri, string capability)
        {
            m_AssetURI = uri;
            m_AssetCapability = capability;
        }
        #endregion

        #region Metadata accessors
        public override Stream this[UUID key]
        {
            get
            {
                try
                {
                    return HttpRequestHandler.DoStreamGetRequest(m_AssetURI + "assets/" + key.ToString() + "/data", null, TimeoutMs);
                }
                catch
                {
                    throw new AssetNotFound(key);
                }
            }
        }
        #endregion
    }
}
