// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Http.Client;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SilverSim.BackendConnectors.Simian.Asset
{
    public sealed class SimianAssetDataConnector : AssetDataServiceInterface
    {
        public int TimeoutMs = 20000;
        private string m_AssetURI;

        #region Constructor
        public SimianAssetDataConnector(string uri)
        {
            m_AssetURI = uri;
        }
        #endregion

        #region Metadata accessors
        [SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule")]
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
                    throw new AssetNotFoundException(key);
                }
            }
        }
        #endregion
    }
}
