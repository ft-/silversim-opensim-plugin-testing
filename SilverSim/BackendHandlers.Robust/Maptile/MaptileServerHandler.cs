// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Maptile;
using SilverSim.Types;
using SilverSim.Types.Maptile;
using SilverSim.Types.StructuredData.REST;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Maptile
{
    public class MaptileServerHandler : IPlugin
    {
        static readonly ILog m_Log = LogManager.GetLogger("ROBUST MAPTILE HANDLER");

        readonly string m_MaptileServiceName;
        MaptileServiceInterface m_MaptileService;
        readonly Regex m_GetRegex = new Regex(@"/^map-([0-9]+)-([0-9]+)-([0-9]+)-.+\\.jpg$/");

        public MaptileServerHandler(IConfig ownSection)
        {
            m_MaptileServiceName = ownSection.GetString("MaptileService", "MaptileService");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for maptile server");
            m_MaptileService = loader.GetService<MaptileServiceInterface>(m_MaptileServiceName);
            loader.HttpServer.StartsWithUriHandlers.Add("/map", MaptileHandler);
            try
            {
                loader.HttpsServer.StartsWithUriHandlers.Add("/map", MaptileHandler);
            }
            catch(ConfigurationLoader.ServiceNotFoundException)
            {

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

            GridVector location = new GridVector(x * 256, y * 256);
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
            Dictionary<string, object> data;
            try
            {
                data = REST.ParseREST(req.Body);
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            UUID scopeid = UUID.Zero;

            uint x;
            uint y;
            byte[] imgdata;
            string type;

            try
            {
                x = uint.Parse(data["X"].ToString()) * 256;
                y = uint.Parse(data["Y"].ToString()) * 256;
                imgdata = Convert.FromBase64String(data["DATA"].ToString());
                type = data["TYPE"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }


            if (data.ContainsKey("SCOPEID") &&
                !UUID.TryParse(data["SCOPEID"].ToString(), out scopeid))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            MaptileData nd = new MaptileData();
            nd.Location = new GridVector(x, y);
            nd.ContentType = type;
            nd.Data = imgdata;
            nd.ScopeID = scopeid;
            nd.ZoomLevel = 1;
            bool success;
            string message = string.Empty;
            try
            {
                m_MaptileService.Store(nd);
                success = true;
            }
            catch(Exception e)
            {
                message = e.Message;
                success = false;
            }
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (Stream s = res.GetOutputStream())
                {
                    using (XmlTextWriter w = s.UTF8XmlTextWriter())
                    {
                        w.WriteStartElement("ServerResponse");
                        w.WriteNamedValue("Result", success);
                        if (!success)
                        {
                            w.WriteNamedValue("Message", message);
                        }
                        w.WriteEndElement();
                    }
                }
            }
        }
    }

    [PluginName("MaptileHandler")]
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