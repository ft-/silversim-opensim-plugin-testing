// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using SilverSim.Http.Client;
using SilverSim.StructuredData.JSON;
using SilverSim.Types;
using SilverSim.Types.Grid;
using System;

namespace SilverSim.Backend.OpenSim.Neighbor.Neighbor
{
    public static class OpenSimNeighborConnector
    {
        static readonly ILog m_Log = LogManager.GetLogger("OPENSIM NEIGHBOR NOTIFIER");

        public static void notifyNeighborStatus(RegionInfo fromRegion, RegionInfo toRegion)
        {
            if (toRegion.ProtocolVariant != RegionInfo.ProtocolVariantId.OpenSim)
            {
                return;
            }
            string uri = toRegion.ServerURI + "region/" + fromRegion.ID + "/";

            Map m = new Map();
            m.Add("region_id", fromRegion.ID);
            m.Add("region_name", fromRegion.Name);
            Uri serverURI = new Uri(fromRegion.ServerURI, UriKind.Absolute);
            m.Add("external_host_name", serverURI.Host);
            m.Add("http_port", fromRegion.ServerHttpPort.ToString());
            m.Add("server_uri", fromRegion.ServerURI);
            m.Add("region_xloc", fromRegion.Location.X.ToString());
            m.Add("region_yloc", fromRegion.Location.Y.ToString());
            m.Add("region_zloc", "0");
            m.Add("region_size_x", fromRegion.Size.X.ToString());
            m.Add("region_size_y", fromRegion.Size.Y.ToString());
            m.Add("region_size_z", "4096");
            m.Add("internal_ep_address", fromRegion.ServerIP.ToString());
            m.Add("internal_ep_port", fromRegion.ServerPort.ToString());
            /* proxy_url is defined but when is it ever used? */
            /* remoting_address is defined but why does the neighbor need to know this data? */
            m.Add("remoting_port", "0");
            m.Add("allow_alt_ports", false);
            /* region_type is defined but when is it ever used? */
            m.Add("destination_handle", toRegion.Location.RegionHandle.ToString());
            m_Log.InfoFormat("notifying neighbor {0} ({1}) of {2} ({3})", toRegion.Name, toRegion.ID, fromRegion.Name, fromRegion.Name);
            Map res = (Map)JSON.Deserialize(HttpRequestHandler.DoStreamRequest("POST", uri, null, "application/json", JSON.Serialize(m), false, 10000));
            if(!res["success"].AsBoolean)
            {
                throw new Exception("notifying neighbor failed");
            }
        }
    }
}
