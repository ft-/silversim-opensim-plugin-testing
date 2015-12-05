// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Http.Client;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types.StructuredData.AssetXml;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System.IO;
using System.Web;
using System;
using System.Net;

namespace SilverSim.BackendConnectors.Robust.Asset
{
    public class RobustAssetMetadataConnector : AssetMetadataServiceInterface
    {
        public int TimeoutMs = 20000;
        readonly string m_AssetURI;

        #region Constructor
        public RobustAssetMetadataConnector(string uri)
        {
            m_AssetURI = uri;
        }
        #endregion

        #region Metadata accessors
        public override bool TryGetValue(UUID key, out AssetMetadata metadata)
        {
            try
            {
                using (Stream stream = HttpRequestHandler.DoStreamGetRequest(m_AssetURI + "assets/" + key.ToString() + "/metadata", null, TimeoutMs))
                {
                    metadata = AssetXml.ParseAssetMetadata(stream);
                    return true;
                }
            }
            catch (HttpException e)
            {
                if (e.GetHttpCode() == (int)HttpStatusCode.NotFound)
                {
                    metadata = default(AssetData);
                    return false;
                }
                throw;
            }
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
