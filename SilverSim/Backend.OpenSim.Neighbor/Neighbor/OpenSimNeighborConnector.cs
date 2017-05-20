// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3 with
// the following clarification and special exception.

// Linking this library statically or dynamically with other modules is
// making a combined work based on this library. Thus, the terms and
// conditions of the GNU Affero General Public License cover the whole
// combination.

// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module. An independent module is a module which is not derived from
// or based on this library. If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so. If you do not wish to do so, delete this
// exception statement from your version.

using log4net;
using SilverSim.Http.Client;
using SilverSim.Types.StructuredData.Json;
using SilverSim.Types;
using SilverSim.Types.Grid;
using System;
using System.IO;

namespace SilverSim.Backend.OpenSim.Neighbor.Neighbor
{
    public static class OpenSimNeighborConnector
    {
        private static readonly ILog m_Log = LogManager.GetLogger("OPENSIM NEIGHBOR NOTIFIER");

        public static void NotifyNeighborStatus(RegionInfo fromRegion, RegionInfo toRegion)
        {
            if (toRegion.ProtocolVariant != RegionInfo.ProtocolVariantId.OpenSim)
            {
                return;
            }
            string uri = toRegion.ServerURI + "region/" + fromRegion.ID.ToString() + "/";

            var serverURI = new Uri(fromRegion.ServerURI, UriKind.Absolute);
            var m = new Map
            {
                { "region_id", fromRegion.ID },
                { "region_name", fromRegion.Name },
                { "external_host_name", serverURI.Host },
                { "http_port", fromRegion.ServerHttpPort.ToString() },
                { "server_uri", fromRegion.ServerURI },
                { "region_xloc", fromRegion.Location.X.ToString() },
                { "region_yloc", fromRegion.Location.Y.ToString() },
                { "region_zloc", "0" },
                { "region_size_x", fromRegion.Size.X.ToString() },
                { "region_size_y", fromRegion.Size.Y.ToString() },
                { "region_size_z", "4096" },
                { "internal_ep_address", fromRegion.ServerIP },
                { "internal_ep_port", fromRegion.ServerPort.ToString() },
                /* proxy_url is defined but when is it ever used? */
                /* remoting_address is defined but why does the neighbor need to know this data? */
                { "remoting_port", "0" },
                { "allow_alt_ports", false },
                /* region_type is defined but when is it ever used? */
                { "destination_handle", toRegion.Location.RegionHandle.ToString() }
            };
            m_Log.InfoFormat("notifying neighbor {0} ({1}) of {2} ({3})", toRegion.Name, toRegion.ID, fromRegion.Name, fromRegion.Name);
            Map res;
            using (Stream resStream = HttpClient.DoStreamRequest("POST", uri, null, "application/json", Json.Serialize(m), false, 10000))
            {
                res = (Map)Json.Deserialize(resStream);
            }
            if(!res["success"].AsBoolean)
            {
                throw new InvalidDataException("notifying neighbor failed");
            }
        }
    }
}
