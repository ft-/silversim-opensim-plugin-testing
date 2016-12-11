// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net.Repository;
using SilverSim.Main.Common;
using Nini.Config;
using SilverSim.ServiceInterfaces.Maptile;
using SilverSim.Main.Common.HttpServer;
using System.Net;
using System.Text.RegularExpressions;
using SilverSim.Types;
using SilverSim.Types.Maptile;
using System.IO;

namespace SilverSim.BackendHandlers.Robust.Maptile
{
    public class MaptileServerHandler : IPlugin
    {
        readonly string m_MaptileServiceName;
        MaptileServiceInterface m_MaptileService;
        Regex m_GetRegex = new Regex(@"/^map-([0-9]+)-([0-9]+)-([0-9]+)-.+\\.jpg$/");

        public MaptileServerHandler(IConfig ownSection)
        {
            m_MaptileServiceName = ownSection.GetString("MaptileService", "MaptileService");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_MaptileService = loader.GetService<MaptileServiceInterface>(m_MaptileServiceName);
            loader.HttpServer.StartsWithUriHandlers.Add("/map", MaptileHandler);
            if (null != loader.HttpsServer)
            {
                loader.HttpsServer.StartsWithUriHandlers.Add("/map", MaptileHandler);
            }
        }

        void MaptileHandler(HttpRequest req)
        {
            if (req.ContainsHeader("X-SecondLife-Shard"))
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Request source not allowed");
                return;
            }

            switch (req.Method)
            {
                case "POST":
                    PostMaptileHandler(req);
                    break;

                case "GET":
                    GetMaptileHandler(req);
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
                    break;
            }
        }

        void GetMaptileHandler(HttpRequest req)
        {
            Match m = m_GetRegex.Match(req.RawUrl);
            if(m == null)
            {
                req.ErrorResponse(HttpStatusCode.NotFound, "Not found");
                return;
            }

            int zoomLevel;
            uint x;
            uint y;
            if(!int.TryParse(m.Groups[1].Value, out zoomLevel) ||
                uint.TryParse(m.Groups[2].Value, out x) ||
                uint.TryParse(m.Groups[3].Value, out y))
            {
                req.ErrorResponse(HttpStatusCode.NotFound, "Not found");
                return;
            }

            GridVector location = new GridVector(x, y);
            MaptileData data;
            if(m_MaptileService.TryGetValue(UUID.Zero, location, zoomLevel, out data))
            {
                using (HttpResponse res = req.BeginResponse(data.ContentType))
                {
                    using (Stream o = res.GetOutputStream(data.Data.Length))
                    {
                        o.Write(data.Data, 0, data.Data.Length);
                    }
                }
            }
            else
            {
                req.ErrorResponse(HttpStatusCode.NotFound, "Not found");
                return;
            }
        }

        void PostMaptileHandler(HttpRequest req)
        {

        }
    }

    [PluginName("MaptileServer")]
    public class MaptileServerHandlerFactory : IPluginFactory
    {
        public MaptileServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new MaptileServerHandler(ownSection);
        }
    }
}