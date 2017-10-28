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
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.Json;
using System.ComponentModel;
using System.IO;
using System.Net;

namespace SilverSim.Backend.OpenSim.Neighbor.Neighbor
{
    [Description("OpenSim Neighbor Protocol Handler")]
    public class OpenSimNeighborHandler : IPlugin
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("OPENSIM NEIGHBOR HANDLER");
        private BaseHttpServer m_HttpServer;
        private readonly OpenSimNeighbor m_NeighborHandler;
        private SceneList m_Scenes;

        public OpenSimNeighborHandler(OpenSimNeighbor neighborHandler)
        {
            m_NeighborHandler = neighborHandler;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Scenes = loader.Scenes;
            m_Log.Info("Initializing handler for /region");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.StartsWithUriHandlers.Add("/region", RegionPostHandler);
            BaseHttpServer https;
            if(loader.TryGetHttpsServer(out https))
            {
                https.StartsWithUriHandlers.Add("/region", RegionPostHandler);
            }
        }

        private void GetRegionParams(string uri, out UUID regionID)
        {
            /* /region/<UUID> */
            regionID = UUID.Zero;

            string trimmed_uri = uri.Trim(new char[] { '/' });
            string[] parts = trimmed_uri.Split('/');
            if(parts.Length < 2)
            {
                throw new InvalidDataException();
            }
            else
            {
                regionID = UUID.Parse(parts[1]);
            }
        }

        public void RegionPostHandler(HttpRequest req)
        {
            if (req.ContainsHeader("X-SecondLife-Shard"))
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Request source not allowed");
                return;
            }

            UUID regionID;
            try
            {
                GetRegionParams(req.RawUrl, out regionID);
            }
            catch
            {
                throw new InvalidDataException();
            }

            if(req.Method == "DELETE" || req.Method == "PUT")
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                return;
            }
            if (req.Method != "POST")
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                return;
            }

            if (req.ContentType != "application/json")
            {
                req.ErrorResponse(HttpStatusCode.UnsupportedMediaType);
                return;
            }

            Map m;
            try
            {
                m = Json.Deserialize(req.Body) as Map;
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if (m == null)
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if (!m.ContainsKey("destination_handle"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            var fromRegion = new RegionInfo();

            try
            {
                fromRegion.ID = m["region_id"].AsUUID;
                if (m.ContainsKey("region_name"))
                {
                    fromRegion.Name = m["region_name"].ToString();
                }
                fromRegion.ServerHttpPort = m["http_port"].AsUInt;
                fromRegion.ServerURI = m["server_uri"].ToString();
                fromRegion.Location.X = m["region_xloc"].AsUInt;
                fromRegion.Location.Y = m["region_yloc"].AsUInt;
                fromRegion.Size.X = m["region_size_x"].AsUInt;
                fromRegion.Size.Y = m["region_size_y"].AsUInt;
#warning check whether to use external_host_name here instead of internal_ep_address
                fromRegion.ServerIP = m["internal_ep_address"].ToString();
                fromRegion.ServerPort = m["internal_ep_port"].AsUInt;
                fromRegion.Flags = RegionFlags.RegionOnline;
                fromRegion.ProtocolVariant = RegionInfo.ProtocolVariantId.OpenSim;
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            SceneInterface scene;

            try
            {
                scene = m_Scenes[new GridVector(m["destination_handle"].AsULong)];
            }
            catch
            {
                m = new Map
                {
                    { "success", false }
                };
                using (HttpResponse resp = req.BeginResponse("application/json"))
                using (Stream outStream = resp.GetOutputStream())
                {
                    Json.Serialize(m, outStream);
                }
                return;
            }

            m = new Map();
            try
            {
                if(fromRegion.ServerURI == scene.ServerURI)
                {
                    fromRegion.ProtocolVariant = RegionInfo.ProtocolVariantId.Local;
                }
                m_NeighborHandler.NotifyRemoteNeighborStatus(fromRegion, scene.ID);
                m.Add("success", true);
            }
            catch
            {
                m_Log.WarnFormat("Failed to notify local neighbor (from {0} (ID {1}) to {2} (ID {3})",
                    fromRegion.Name, fromRegion.ID,
                    scene.Name, scene.ID);
                m.Add("success", false);
            }

            using (HttpResponse resp = req.BeginResponse("application/json"))
            using (Stream outStream = resp.GetOutputStream())
            {
                Json.Serialize(m, outStream);
            }
        }
    }
}
