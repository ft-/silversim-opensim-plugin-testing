// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using System.Net;
using System.Text;

namespace SilverSim.BackendHandlers.Robust.Helo
{
    #region Service implementation
    class RobustHeloServerHandler : IPlugin
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST HELO HANDLER");
        private BaseHttpServer m_HttpServer;
        private static Encoding UTF8NoBOM = new System.Text.UTF8Encoding(false);

        public RobustHeloServerHandler()
        {
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing HELO handler");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.StartsWithUriHandlers.Add("/helo", HeloHandler);
        }

        public void HeloHandler(HttpRequest req)
        {
            switch (req.Method)
            {
                case "GET": case "HEAD":
                    HttpResponse res = req.BeginResponse();
                    res.ContentType = "text/plain";
                    res.Headers.Add("X-Handlers-Provided", "opensim-robust");
                    res.Close();
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
                    break;
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("HeloHandler")]
    public class RobustHeloHandlerFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST HELO HANDLER");
        public RobustHeloHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustHeloServerHandler();
        }
    }
    #endregion
}
