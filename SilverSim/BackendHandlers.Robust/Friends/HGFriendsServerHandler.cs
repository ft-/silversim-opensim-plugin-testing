using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.Types;
using SilverSim.Types.StructuredData.REST;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Friends
{
    [Description("Robust Friends Protocol Server")]
    public class HGFriendsServerHandler : IPlugin, IServiceURLsGetInterface
    {
        FriendsServiceInterface m_FriendsService;
        readonly string m_FriendsServiceName;
        BaseHttpServer m_HttpServer;

        readonly Dictionary<string, Action<HttpRequest, Dictionary<string, object>>> m_Handlers = new Dictionary<string, Action<HttpRequest, Dictionary<string, object>>>();

        public HGFriendsServerHandler(IConfig ownSection)
        {
            m_FriendsServiceName = ownSection.GetString("FriendsService", "FriendsService");
        }

        public void GetServiceURLs(Dictionary<string, string> dict)
        {
            dict.Add("FriendsServerURI", m_HttpServer.ServerURI);
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_HttpServer = loader.HttpServer;
            m_FriendsService = loader.GetService<FriendsServiceInterface>(m_FriendsServiceName);
            m_HttpServer.UriHandlers.Add("/hgfriends", HGFriendsHandler);
        }

        void HGFriendsHandler(HttpRequest httpreq)
        {
            if (httpreq.ContainsHeader("X-SecondLife-Shard"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Request source not allowed");
                return;
            }

            if (httpreq.Method != "POST")
            {
                httpreq.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
                return;
            }

            Dictionary<string, object> reqdata;
            try
            {
                reqdata = REST.ParseREST(httpreq.Body);
            }
            catch
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            if (!reqdata.ContainsKey("METHOD"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Missing 'METHOD' field");
                return;
            }

            Action<HttpRequest, Dictionary<string, object>> del;
            try
            {
                if (m_Handlers.TryGetValue(reqdata["METHOD"].ToString(), out del))
                {
                    del(httpreq, reqdata);
                }
                else
                {
                    throw new FailureResultException();
                }
            }
            catch (FailureResultException e)
            {
                using (HttpResponse res = httpreq.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteNamedValue("RESULT", "Failure");
                        writer.WriteNamedValue("Message", e.Message);
                        writer.WriteEndElement();
                    }
                }
            }
            catch
            {
                if (httpreq.Response != null)
                {
                    httpreq.Response.Close();
                }
                else
                {
                    using (HttpResponse res = httpreq.BeginResponse("text/xml"))
                    {
                        using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                        {
                            writer.WriteStartElement("ServerResponse");
                            writer.WriteNamedValue("RESULT", "Failure");
                            writer.WriteNamedValue("Message", string.Empty);
                            writer.WriteEndElement();
                        }
                    }
                }
            }
        }

    }

    [PluginName("HGFriendsHandler")]
    public class HGFriendsServerHandlerFactory : IPluginFactory
    {
        public HGFriendsServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new HGFriendsServerHandler(ownSection);
        }
    }
}
