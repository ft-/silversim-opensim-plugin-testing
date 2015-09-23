// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Avatar;
using SilverSim.Types;
using SilverSim.Types.StructuredData.REST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Avatar
{
    static class RobustAvatarServiceExtensionMethods
    {
        public static Dictionary<string, string> ToAvatarData(this Dictionary<string, object> reqdata)
        {
            Dictionary<string, string> resdata = new Dictionary<string, string>();

            foreach(string key in reqdata.Keys)
            {
                switch(key)
                {
                    case "UserID":
                        break;

                    case "VERSIONMAX":
                        break;

                    case "VERSIONMIN":
                        break;

                    case "METHOD":
                        break;

                    default:
                        resdata[key.ToString().Replace("_", " ")] = reqdata.GetString(key);
                        break;
                }
            }

            return resdata;
        }
    }

    #region Service Implementation
    class RobustAvatarServerHandler : IPlugin
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST AVATAR HANDLER");
        private BaseHttpServer m_HttpServer;
        AvatarServiceInterface m_AvatarService = null;
        string m_AvatarServiceName;
        private static Encoding UTF8NoBOM = new System.Text.UTF8Encoding(false);

        public RobustAvatarServerHandler(string avatarServiceName)
        {
            m_AvatarServiceName = avatarServiceName;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for avatar server");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.StartsWithUriHandlers.Add("/avatar", AvatarHandler);
            m_AvatarService = loader.GetService<AvatarServiceInterface>(m_AvatarServiceName);
        }

        void SuccessResult(HttpRequest req)
        {
            HttpResponse res = req.BeginResponse("text/xml");
            using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteStartElement("result");
                writer.WriteValue("Success");
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            res.Close();
        }

        public void AvatarHandler(HttpRequest req)
        {
            if (req.ContainsHeader("X-SecondLife-Shard"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Request source not allowed");
                return;
            }

            if (req.Method != "POST")
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
                return;
            }

            Dictionary<string, object> data;
            try
            {
                data = REST.parseREST(req.Body);
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            if(!data.ContainsKey("METHOD"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            try
            {
                switch (data["METHOD"].ToString())
                {
                    case "getavatar":
                        GetAvatar(req, data);
                        break;

                    case "resetavatar":
                        ResetAvatar(req, data);
                        break;

                    case "removeitems":
                        RemoveItems(req, data);
                        break;

                    case "setavatar":
                        SetAvatar(req, data);
                        break;

                    case "setitems":
                        SetItems(req, data);
                        break;

                    default:
                        req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                        break;
                }
            }
            catch(FailureResultException)
            {
                HttpResponse res = req.BeginResponse("text/xml");
                using(XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("result");
                    writer.WriteValue("Failure");
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                res.Close();
            }
        }

        void GetAvatar(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID avatarID = reqdata.GetUUID("UserID");
            Dictionary<string, string> result;
            try
            {
                result = m_AvatarService[avatarID];
            }
            catch
            {
                throw new FailureResultException();
            }
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
                {
                    writer.WriteStartElement("ServerResponse");
                    {
                        writer.WriteStartElement("result");
                        writer.WriteAttributeString("type", "List");
                        foreach (KeyValuePair<string, string> kvp in result)
                        {
                            writer.WriteNamedValue(XmlConvert.EncodeLocalName(kvp.Key), kvp.Value);
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
            }
        }

        void ResetAvatar(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID avatarID = reqdata.GetUUID("UserID");
            try
            {
                m_AvatarService[avatarID] = null;
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(req);
        }

        void RemoveItems(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID avatarID = reqdata.GetUUID("UserID");
            List<string> names = reqdata.GetList("Names");
            try
            {
                m_AvatarService.Remove(avatarID, names);
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(req);
        }

        void SetAvatar(HttpRequest req, Dictionary<string, object> reqdata)
        {
            Dictionary<string, string> avatarData = reqdata.ToAvatarData();
            UUID principalID = reqdata.GetUUID("UserID");
            try
            {
                m_AvatarService[principalID] = avatarData;
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(req);
        }

        void SetItems(HttpRequest req, Dictionary<string, object> reqdata)
        {
            List<string> names = reqdata.GetList("Names");
            List<string> values = reqdata.GetList("Values");
            UUID principalID = reqdata.GetUUID("UserID");
            try
            {
                m_AvatarService[principalID, names] = values;
            }
            catch
            {
                throw new FailureResultException();
            }
            SuccessResult(req);
        }
    }
    #endregion

    #region Factory
    [PluginName("AvatarHandler")]
    public class RobustAvatarHandlerFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST AVATAR HANDLER");
        public RobustAvatarHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustAvatarServerHandler(ownSection.GetString("AvatarService", "AvatarService"));
        }
    }
    #endregion
}
