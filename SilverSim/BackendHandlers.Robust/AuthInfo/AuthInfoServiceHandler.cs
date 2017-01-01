// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.AuthInfo;
using SilverSim.Types;
using SilverSim.Types.AuthInfo;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.Net;
using System.Xml;
using System;
using System.ComponentModel;

namespace SilverSim.BackendHandlers.Robust.AuthInfo
{
    #region Service implementation
    [Description("Robust AuthInfo Protocol Server")]
    public class AuthInfoServiceHandler : IPlugin, IHttpAclListAccess
    {
        readonly string m_AuthInfoServiceName;
        AuthInfoServiceInterface m_AuthInfoService;
        readonly List<HttpAclHandler> m_AclHandlers = new List<HttpAclHandler>();
        readonly HttpAclHandler m_SetAuthInfoAcl = new HttpAclHandler("setauthinfo");
        readonly HttpAclHandler m_GetAuthInfoAcl = new HttpAclHandler("getauthinfo");
        readonly HttpAclHandler m_SetPasswordAcl = new HttpAclHandler("setpassword");

        public HttpAclHandler[] HttpAclLists
        {
            get
            {
                return m_AclHandlers.ToArray();
            }
        }

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
            try
            {
                loader.HttpsServer.UriHandlers.Add("/auth/plain", HandlePlainRequests);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        public void HandlePlainRequests(HttpRequest httpreq)
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

        void SuccessResponse(HttpRequest httpreq, string token)
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

        void SuccessResponse(HttpRequest httpreq)
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

        void HandleAuthenticate(HttpRequest req, Dictionary<string, object> data)
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

        void HandleSetPassword(HttpRequest req, Dictionary<string, object> data)
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

        void HandleVerify(HttpRequest req, Dictionary<string, object> data)
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

        void HandleRelease(HttpRequest req, Dictionary<string, object> data)
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

        void HandleGetAuthInfo(HttpRequest req, Dictionary<string, object> data)
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

        void HandleSetAuthInfo(HttpRequest req, Dictionary<string, object> data)
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
    #endregion

    #region Service factory
    [PluginName("AuthInfoHandler")]
    public class AuthInfoServiceHandlerFactory : IPluginFactory
    {
        public AuthInfoServiceHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new AuthInfoServiceHandler(ownSection);
        }
    }
    #endregion
}
