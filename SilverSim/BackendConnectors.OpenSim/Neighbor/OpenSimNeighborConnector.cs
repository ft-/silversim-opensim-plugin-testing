// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Neighbor;
using SilverSim.StructuredData.JSON;
using SilverSim.Types;
using SilverSim.Types.Grid;
using System;

namespace SilverSim.BackendConnectors.OpenSim.Neighbor
{
    #region Service Implementation
    public class OpenSimNeighborConnector : NeighborServiceInterface, IPlugin
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("OPENSIM NEIGHBOR NOTIFIER");

        string m_GridURI;

        public OpenSimNeighborConnector(string gridURI)
        {
            m_GridURI = gridURI;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initialized");
        }

        public override void notifyNeighborStatus(RegionInfo fromRegion, RegionInfo toRegion)
        {
            if (toRegion.ProtocolVariant != RegionInfo.ProtocolVariantId.OpenSim || toRegion.GridURI != m_GridURI)
            {
                return;
            }
            string uri = toRegion.ServerURI + "region/" + fromRegion.ID + "/";

            Map m = new Map();
            m.Add("region_id", fromRegion.ID);
            m.Add("region_name", fromRegion.Name);
            try
            {
                Uri serverURI = new Uri(fromRegion.ServerURI, UriKind.Absolute);
                m.Add("external_host_name", serverURI.Host);
            }
            catch
            {
                return;
            }
            m.Add("http_port", fromRegion.ServerHttpPort.ToString());
            m.Add("server_uri", fromRegion.ServerURI);
            m.Add("region_xloc", fromRegion.Location.X.ToString());
            m.Add("region_yloc", fromRegion.Location.Y.ToString());
            m.Add("region_xloc", "0");
            m.Add("region_size_x", fromRegion.Size.X.ToString());
            m.Add("region_size_y", fromRegion.Size.Y.ToString());
            m.Add("region_size_x", "4096");
            m.Add("internal_ep_address", fromRegion.ServerIP.ToString());
            m.Add("internal_ep_port", fromRegion.ServerPort.ToString());
            /* proxy_url is defined but when is it ever used? */
            /* remoting_address is defined but why does the neighbor need to know this data? */
            m.Add("remoting_port", "0");
            m.Add("allow_alt_ports", false);
            /* region_type is defined but when is it ever used? */
            m.Add("destination_handle", toRegion.Location.RegionHandle.ToString());
            try
            {
                HttpRequestHandler.DoRequest("POST", uri, null, "application/json", JSON.Serialize(m), false, 10000);
            }
            catch
            {

            }
        }

        public override ServiceTypeEnum ServiceType
        {
            get
            {
                return NeighborServiceInterface.ServiceTypeEnum.Remote;
            }
        }
    }
    #endregion

    #region Service Factory
    [PluginName("OpenSimNeighborConnector")]
    public class OpenSimNeighborConnectorFactory : IPluginFactory
    {
        public OpenSimNeighborConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new OpenSimNeighborConnector("");
        }
    }
    #endregion
}
