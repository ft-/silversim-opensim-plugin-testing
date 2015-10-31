// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types.StructuredData.Json;
using SilverSim.Types;
using SilverSim.Types.Grid;
using System.IO;
using System.Net;

namespace SilverSim.Backend.OpenSim.Neighbor.Neighbor
{
    public class OpenSimNeighborHandler : IPlugin
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("OPENSIM NEIGHBOR HANDLER");
        private BaseHttpServer m_HttpServer;
        OpenSimNeighbor m_NeighborHandler;

        public OpenSimNeighborHandler(OpenSimNeighbor neighborHandler)
        {
            m_NeighborHandler = neighborHandler;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for /region");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.StartsWithUriHandlers.Add("/region", RegionPostHandler);
        }

        private void GetRegionParams(string uri, out UUID regionID)
        {
            /* /region/<UUID> */
            regionID = UUID.Zero;

            uri = uri.Trim(new char[] { '/' });
            string[] parts = uri.Split('/');
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
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed");
                return;
            }
            if (req.Method != "POST")
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed");
                return;
            }

            if (req.ContentType != "application/json")
            {
                req.ErrorResponse(HttpStatusCode.UnsupportedMediaType, "Unsupported media type");
                return;
            }

            Map m;
            try
            {
                m = Json.Deserialize(req.Body) as Map;
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            if (null == m)
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            if (!m.ContainsKey("destination_handle"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            RegionInfo fromRegion = new RegionInfo();
            HttpResponse resp;

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
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            SceneInterface scene;

            try
            {
                scene = SceneManager.Scenes[new GridVector(m["destination_handle"].AsULong)];
            }
            catch
            {
                m = new Map();
                m.Add("success", false);
                resp = req.BeginResponse();
                resp.ContentType = "application/json";
                Json.Serialize(m, resp.GetOutputStream());
                resp.Close();
                return;
            }

            m = new Map();
            try
            {
                if(fromRegion.ServerURI == scene.RegionData.ServerURI)
                {
                    fromRegion.ProtocolVariant = RegionInfo.ProtocolVariantId.Local;
                }
                m_NeighborHandler.notifyRemoteNeighborStatus(fromRegion, scene.ID);
                m.Add("success", true);
            }
            catch
            {
                m_Log.WarnFormat("Failed to notify local neighbor (from {0} (ID {1}) to {2} (ID {3})",
                    fromRegion.Name, fromRegion.ID,
                    scene.Name, scene.ID);
                m.Add("success", false);
            }

            resp = req.BeginResponse();
            resp.ContentType = "application/json";
            Json.Serialize(m, resp.GetOutputStream());
            resp.Close();
        }
    }
}
