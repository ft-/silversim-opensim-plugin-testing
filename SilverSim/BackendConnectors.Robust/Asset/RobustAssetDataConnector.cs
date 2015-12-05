// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Http.Client;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System;

namespace SilverSim.BackendConnectors.Robust.Asset
{
    public class RobustAssetDataConnector : AssetDataServiceInterface
    {
        public int TimeoutMs = 20000;
        readonly string m_AssetURI;

        #region Constructor
        public RobustAssetDataConnector(string uri)
        {
            m_AssetURI = uri;
        }
        #endregion

        #region Metadata accessors
        public override bool TryGetValue(UUID key, out Stream s)
        {
            try
            {
                s = HttpRequestHandler.DoStreamGetRequest(m_AssetURI + "assets/" + key.ToString() + "/data", null, TimeoutMs);
                return true;
            }
            catch
            {
                s = null;
                return false;
            }
        }

        [SuppressMessage("Gendarme.Rules.Correctness", "EnsureLocalDisposalRule")]
        public override Stream this[UUID key]
        {
            get
            {
                Stream s;
                if(!TryGetValue(key, out s))
                {
                    throw new AssetNotFoundException(key);
                }
                return s;
            }
        }
        #endregion
    }
}
