// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Asset
{
    public class RobustAssetDataConnector : AssetDataServiceInterface
    {
        public int TimeoutMs = 20000;
        private string m_AssetURI;

        #region Constructor
        public RobustAssetDataConnector(string uri)
        {
            m_AssetURI = uri;
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
