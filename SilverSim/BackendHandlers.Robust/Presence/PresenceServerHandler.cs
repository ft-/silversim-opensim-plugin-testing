// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.ServiceInterfaces.Traveling;
using SilverSim.Types;
using SilverSim.Types.Presence;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Presence
{
    #region Service Implementation
    [Description("Robust Presence Protocol Server")]
    public sealed class RobustPresenceServerHandler : IPlugin
    {
        PresenceServiceInterface m_PresenceService;
        TravelingDataServiceInterface m_TravelingDataService;
        readonly string m_TravelingDataServiceName;
        readonly string m_PresenceServiceName;
        BaseHttpServer m_HttpServer;

        public RobustPresenceServerHandler(IConfig ownSection)
        {
            m_PresenceServiceName = ownSection.GetString("PresenceService", "PresenceService");
            m_TravelingDataServiceName = ownSection.GetString("TravelingDataService", "TravelingDataService");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_PresenceService = loader.GetService<PresenceServiceInterface>(m_PresenceServiceName);
            m_TravelingDataService = loader.GetService<TravelingDataServiceInterface>(m_TravelingDataServiceName);
            m_HttpServer = loader.HttpServer;
            m_HttpServer.UriHandlers.Add("/presence", PresenceHandler);
            try
            {
                loader.HttpsServer.UriHandlers.Add("/presence", PresenceHandler);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        void FailureResult(HttpRequest req)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteNamedValue("result", "Failure");
                    writer.WriteEndElement();
                }
            }
        }

        void SuccessResult(HttpRequest req)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteNamedValue("result", "Success");
                    writer.WriteEndElement();
                }
            }
        }

        void PresenceHandler(HttpRequest httpreq)
        {
            if (httpreq.ContainsHeader("X-SecondLife-Shard"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Request source not allowed");
                return;
            }

            if (httpreq.Method != "POST")
            {
                httpreq.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                return;
            }

            Dictionary<string, object> reqdata;
            try
            {
                reqdata = REST.ParseREST(httpreq.Body);
            }
            catch
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if (!reqdata.ContainsKey("METHOD"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Missing 'METHOD' field");
                return;
            }

            switch (reqdata["METHOD"].ToString())
            {
                case "login":
                    HandleLogin(httpreq, reqdata);
                    break;

                case "logout":
                    HandleLogout(httpreq, reqdata);
                    break;

                case "logoutregion":
                    HandleLogoutRegion(httpreq, reqdata);
                    break;

                case "report":
                    HandleReport(httpreq, reqdata);
                    break;

                case "getagent":
                    HandleGetAgent(httpreq, reqdata);
                    break;

                case "getagents":
                    HandleGetAgents(httpreq, reqdata);
                    break;

                default:
                    FailureResult(httpreq);
                    break;
            }
        }

        void HandleLogin(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            object o;
            PresenceInfo pInfo = new PresenceInfo();
            if(!reqdata.TryGetValue("UserID", out o) || !UUI.TryParse(o.ToString(), out pInfo.UserID))
            {
                FailureResult(httpreq);
                return;
            }
            if(!reqdata.TryGetValue("SessionID", out o) || !UUID.TryParse(o.ToString(), out pInfo.SessionID))
            {
                FailureResult(httpreq);
                return;
            }

            if(!reqdata.TryGetValue("SecureSessionID", out o) || !UUID.TryParse(o.ToString(), out pInfo.SecureSessionID))
            {
                pInfo.SecureSessionID = UUID.Zero;
            }

            try
            {
                m_PresenceService[pInfo.SessionID, pInfo.UserID.ID, PresenceServiceInterface.SetType.Login] = pInfo;
            }
            catch
            {
                FailureResult(httpreq);
                return;
            }
            SuccessResult(httpreq);
        }

        void HandleLogout(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID sessionID;
            object o;
            if (!reqdata.TryGetValue("SessionID", out o) || !UUID.TryParse(o.ToString(), out sessionID))
            {
                FailureResult(req);
                return;
            }

            try
            {
                m_PresenceService[sessionID, UUID.Zero] = null;
                m_TravelingDataService.Remove(sessionID);
            }
            catch
            {
                FailureResult(req);
                return;
            }
            SuccessResult(req);
        }

        void HandleLogoutRegion(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID regionID;
            object o;
            if (!reqdata.TryGetValue("RegionID", out o) || !UUID.TryParse(o.ToString(), out regionID))
            {
                FailureResult(req);
                return;
            }

            try
            {
                List<PresenceInfo> pInfos = m_PresenceService.GetPresencesInRegion(regionID);
                m_PresenceService.LogoutRegion(regionID);
                foreach(PresenceInfo pInfo in pInfos)
                {
                    m_TravelingDataService.Remove(pInfo.SessionID);
                }
            }
            catch
            {
                FailureResult(req);
                return;
            }
            SuccessResult(req);
        }

        void HandleReport(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID sessionID;
            UUID regionID;
            object o;
            if (!reqdata.TryGetValue("SessionID", out o) || !UUID.TryParse(o.ToString(), out sessionID))
            {
                FailureResult(req);
                return;
            }
            if (!reqdata.TryGetValue("RegionID", out o) || !UUID.TryParse(o.ToString(), out regionID))
            {
                FailureResult(req);
                return;
            }

            try
            {
                PresenceInfo pInfo = m_PresenceService[sessionID, UUID.Zero];
                pInfo.RegionID = regionID;
                m_PresenceService[sessionID, pInfo.UserID.ID, PresenceServiceInterface.SetType.Report] = pInfo;
            }
            catch
            {
                FailureResult(req);
                return;
            }
            SuccessResult(req);
        }

        void HandleGetAgent(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID sessionID;
            object o;
            if (!reqdata.TryGetValue("SessionID", out o) || !UUID.TryParse(o.ToString(), out sessionID))
            {
                FailureResult(req);
                return;
            }

            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    PresenceInfo pInfo;
                    try
                    {
                        pInfo = m_PresenceService[sessionID, UUID.Zero];
                        writer.WriteStartElement("result");
                        writer.WriteAttributeString("type", "List");
                        writer.WriteNamedValue("UserID", pInfo.UserID.ToString());
                        writer.WriteNamedValue("RegionID", pInfo.RegionID);
                        writer.WriteEndElement();
                    }
                    catch
                    {
                        writer.WriteNamedValue("result", "null");
                    }

                    writer.WriteEndElement();
                }
            }
        }

        void HandleGetAgents(HttpRequest req, Dictionary<string, object> reqdata)
        {
            object o;
            if (!reqdata.TryGetValue("uuids", out o) || !(o is List<string>))
            {
                FailureResult(req);
                return;
            }
            List<string> uuids = o as List<string>;
            List<UUI> uuis = new List<UUI>();
            foreach(string s in uuids)
            {
                UUI uui;
                if(UUI.TryParse(s, out uui))
                {
                    uuis.Add(uui);
                }
            }

            List<PresenceInfo> results = new List<PresenceInfo>();
            foreach(UUI uui in uuis)
            {
                results.AddRange(m_PresenceService[uui.ID]);
            }

            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    int index = 0;
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("result");
                    writer.WriteAttributeString("type", "List");
                    foreach(PresenceInfo pInfo in results)
                    {
                        writer.WriteStartElement("presence" + index.ToString());
                        writer.WriteAttributeString("type", "List");
                        writer.WriteNamedValue("UserID", pInfo.UserID.ToString());
                        writer.WriteNamedValue("RegionID", pInfo.RegionID);
                        writer.WriteEndElement();
                        ++index;
                    }
                    writer.WriteEndElement();
                }
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("PresenceHandler")]
    public sealed class RobustPresenceServerHandlerFactory : IPluginFactory
    {
        public RobustPresenceServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustPresenceServerHandler(ownSection);
        }
    }
    #endregion
}
