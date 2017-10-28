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
    [Description("Robust Presence Protocol Server")]
    [PluginName("PresenceHandler")]
    public sealed class RobustPresenceServerHandler : IPlugin
    {
        private PresenceServiceInterface m_PresenceService;
        private TravelingDataServiceInterface m_TravelingDataService;
        private readonly string m_TravelingDataServiceName;
        private readonly string m_PresenceServiceName;
        private BaseHttpServer m_HttpServer;

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
            BaseHttpServer https;
            if(loader.TryGetHttpsServer(out https))
            {
                https.UriHandlers.Add("/presence", PresenceHandler);
            }
        }

        private void FailureResult(HttpRequest req)
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

        private void SuccessResult(HttpRequest req)
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

        private void PresenceHandler(HttpRequest httpreq)
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

        private void HandleLogin(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            object o;
            var pInfo = new PresenceInfo();
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
                m_PresenceService.Login(pInfo);
            }
            catch
            {
                FailureResult(httpreq);
                return;
            }
            SuccessResult(httpreq);
        }

        private void HandleLogout(HttpRequest req, Dictionary<string, object> reqdata)
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
                m_PresenceService.Logout(sessionID, UUID.Zero);
                m_TravelingDataService.Remove(sessionID);
            }
            catch
            {
                FailureResult(req);
                return;
            }
            SuccessResult(req);
        }

        private void HandleLogoutRegion(HttpRequest req, Dictionary<string, object> reqdata)
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

        private void HandleReport(HttpRequest req, Dictionary<string, object> reqdata)
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
                m_PresenceService.Report(pInfo);
            }
            catch
            {
                FailureResult(req);
                return;
            }
            SuccessResult(req);
        }

        private void HandleGetAgent(HttpRequest req, Dictionary<string, object> reqdata)
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

        private void HandleGetAgents(HttpRequest req, Dictionary<string, object> reqdata)
        {
            object o;
            if (!reqdata.TryGetValue("uuids", out o) || !(o is List<string>))
            {
                FailureResult(req);
                return;
            }
            var uuids = o as List<string>;
            var uuis = new List<UUI>();
            foreach(string s in uuids)
            {
                UUI uui;
                if(UUI.TryParse(s, out uui))
                {
                    uuis.Add(uui);
                }
            }

            var results = new List<PresenceInfo>();
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
}
