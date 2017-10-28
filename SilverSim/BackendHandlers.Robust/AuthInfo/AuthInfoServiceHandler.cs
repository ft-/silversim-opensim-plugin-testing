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
using SilverSim.ServiceInterfaces.AuthInfo;
using SilverSim.Types;
using SilverSim.Types.AuthInfo;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.AuthInfo
{
    [Description("Robust AuthInfo Protocol Server")]
    [PluginName("AuthInfoHandler")]
    public class AuthInfoServiceHandler : IPlugin, IHttpAclListAccess
    {
        private readonly string m_AuthInfoServiceName;
        private AuthInfoServiceInterface m_AuthInfoService;
        private readonly List<HttpAclHandler> m_AclHandlers = new List<HttpAclHandler>();
        private readonly HttpAclHandler m_SetAuthInfoAcl = new HttpAclHandler("setauthinfo");
        private readonly HttpAclHandler m_GetAuthInfoAcl = new HttpAclHandler("getauthinfo");
        private readonly HttpAclHandler m_SetPasswordAcl = new HttpAclHandler("setpassword");

        public HttpAclHandler[] HttpAclLists => m_AclHandlers.ToArray();

        public AuthInfoServiceHandler(IConfig ownSection)
        {
            m_AclHandlers.Add(m_SetAuthInfoAcl);
            m_AclHandlers.Add(m_GetAuthInfoAcl);
            m_AclHandlers.Add(m_SetPasswordAcl);
            m_AuthInfoServiceName = ownSection.GetString("AuthInfoService", "AuthInfoService");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_AuthInfoService = loader.GetService<AuthInfoServiceInterface>(m_AuthInfoServiceName);
            loader.HttpServer.UriHandlers.Add("/auth/plain", HandlePlainRequests);
            BaseHttpServer https;
            if(loader.TryGetHttpsServer(out https))
            {
                https.UriHandlers.Add("/auth/plain", HandlePlainRequests);
            }
        }

        private void HandlePlainRequests(HttpRequest httpreq)
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

            Dictionary<string, object> data;
            try
            {
                data = REST.ParseREST(httpreq.Body);
            }
            catch
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if (!data.ContainsKey("METHOD") || !data.ContainsKey("PRINCIPAL"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                switch (data["METHOD"].ToString())
                {
                    case "authenticate":
                        HandleAuthenticate(httpreq, data);
                        break;

                    case "setpassword":
                        if(m_SetPasswordAcl.CheckIfAllowed(httpreq))
                        {
                            HandleSetPassword(httpreq, data);
                        }
                        else
                        {
                            httpreq.ErrorResponse(HttpStatusCode.BadRequest);
                        }
                        break;

                    case "verify":
                        HandleVerify(httpreq, data);
                        break;

                    case "release":
                        HandleRelease(httpreq, data);
                        break;

                    case "getauthinfo":
                        if(m_GetAuthInfoAcl.CheckIfAllowed(httpreq))
                        {
                            HandleGetAuthInfo(httpreq, data);
                        }
                        else
                        {
                            httpreq.ErrorResponse(HttpStatusCode.BadRequest);
                        }
                        break;

                    case "setauthinfo":
                        if (m_SetAuthInfoAcl.CheckIfAllowed(httpreq))
                        {
                            HandleSetAuthInfo(httpreq, data);
                        }
                        else
                        {
                            httpreq.ErrorResponse(HttpStatusCode.BadRequest);
                        }
                        break;

                    default:
                        httpreq.ErrorResponse(HttpStatusCode.BadRequest);
                        break;
                }
            }
            catch (FailureResultException)
            {
                using (HttpResponse res = httpreq.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteStartElement("Result");
                        writer.WriteValue("Failure");
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
            }
        }

        private void SuccessResponse(HttpRequest httpreq, string token)
        {
            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteNamedValue("Result", "Success");
                    writer.WriteNamedValue("Token", token);
                    writer.WriteEndElement();
                }
            }
        }

        private void SuccessResponse(HttpRequest httpreq)
        {
            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteNamedValue("Result", "Success");
                    writer.WriteEndElement();
                }
            }
        }

        private void HandleAuthenticate(HttpRequest req, Dictionary<string, object> data)
        {
            UUID id;
            if(!UUID.TryParse(data["PRINCIPAL"].ToString(), out id))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            object password;
            if (!data.TryGetValue("PASSWORD", out password))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            int lifetime = 30;

            if (data.ContainsKey("LIFETIME"))
            {
                lifetime = int.Parse(data["LIFETIME"].ToString());
                if (lifetime > 30)
                {
                    lifetime = 30;
                }
            }

            UUID token = m_AuthInfoService.Authenticate(UUID.Zero, id, password.ToString(), lifetime);
            SuccessResponse(req, token.ToString());
        }

        private void HandleSetPassword(HttpRequest req, Dictionary<string, object> data)
        {
            UUID id;
            object password;
            if(!data.TryGetValue("PASSWORD", out password))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            if (!UUID.TryParse(data["PRINCIPAL"].ToString(), out id))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            m_AuthInfoService.SetPassword(id, password.ToString());
            SuccessResponse(req);
        }

        private void HandleVerify(HttpRequest req, Dictionary<string, object> data)
        {
            UUID id;
            if (!UUID.TryParse(data["PRINCIPAL"].ToString(), out id))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            if (!data.ContainsKey("TOKEN"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            UUID token;
            if (!UUID.TryParse(data["TOKEN"].ToString(), out token))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            int lifetime = 30;

            if(data.ContainsKey("LIFETIME"))
            {
                lifetime = int.Parse(data["LIFETIME"].ToString());
                if (lifetime > 30)
                {
                    lifetime = 30;
                }
            }

            m_AuthInfoService.VerifyToken(id, token, lifetime);
            SuccessResponse(req);
        }

        private void HandleRelease(HttpRequest req, Dictionary<string, object> data)
        {
            UUID id;
            if (!UUID.TryParse(data["PRINCIPAL"].ToString(), out id))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            if (!data.ContainsKey("TOKEN"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            UUID token;
            if (!UUID.TryParse(data["TOKEN"].ToString(), out token))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            m_AuthInfoService.ReleaseToken(id, token);
            SuccessResponse(req);
        }

        private void HandleGetAuthInfo(HttpRequest req, Dictionary<string, object> data)
        {
            UserAuthInfo ai;
            UUID id;
            if (!UUID.TryParse(data["PRINCIPAL"].ToString(), out id))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            ai = m_AuthInfoService[id];

            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("Result");
                    writer.WriteAttributeString("type", "List");
                    writer.WriteNamedValue("PrincipalID", ai.ID.ToString());
                    writer.WriteNamedValue("AccountType", string.Empty);
                    writer.WriteNamedValue("PasswordHash", ai.PasswordHash);
                    writer.WriteNamedValue("PasswordSalt", ai.PasswordSalt);
                    writer.WriteNamedValue("WebLoginKey", string.Empty);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
        }

        private void HandleSetAuthInfo(HttpRequest req, Dictionary<string, object> data)
        {
            UserAuthInfo ai;
            UUID id;
            if (!UUID.TryParse(data["PRINCIPAL"].ToString(), out id))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            ai = m_AuthInfoService[id];
            object o;
            if(data.TryGetValue("PasswordHash", out o))
            {
                ai.PasswordHash = o.ToString();
            }
            if (data.TryGetValue("PasswordSalt", out o))
            {
                ai.PasswordSalt = o.ToString();
            }
            m_AuthInfoService.Store(ai);
            SuccessResponse(req);
        }
    }
}
