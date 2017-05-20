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

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Avatar;
using SilverSim.Types;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Avatar
{
    internal static class RobustAvatarServiceExtensionMethods
    {
        public static Dictionary<string, string> ToAvatarData(this Dictionary<string, object> reqdata)
        {
            var resdata = new Dictionary<string, string>();

            foreach(string key in reqdata.Keys)
            {
                switch(key)
                {
                    case "UserID":
                    case "VERSIONMAX":
                    case "VERSIONMIN":
                    case "METHOD":
                        break;

                    default:
                        resdata[key.Replace('_', ' ')] = reqdata.GetString(key);
                        break;
                }
            }

            return resdata;
        }
    }

    #region Service Implementation
    [Description("Robust Avatar Protocol Server")]
    public class RobustAvatarServerHandler : IPlugin
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST AVATAR HANDLER");
        private BaseHttpServer m_HttpServer;
        private AvatarServiceInterface m_AvatarService;
        private readonly string m_AvatarServiceName;

        public RobustAvatarServerHandler(string avatarServiceName)
        {
            m_AvatarServiceName = avatarServiceName;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for avatar server");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.UriHandlers.Add("/avatar", AvatarHandler);
            m_AvatarService = loader.GetService<AvatarServiceInterface>(m_AvatarServiceName);
            try
            {
                loader.HttpsServer.UriHandlers.Add("/avatar", AvatarHandler);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        private void SuccessResult(HttpRequest req)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("result");
                    writer.WriteValue("Success");
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
        }

        private void AvatarHandler(HttpRequest req)
        {
            if (req.ContainsHeader("X-SecondLife-Shard"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Request source not allowed");
                return;
            }

            if (req.Method != "POST")
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                return;
            }

            Dictionary<string, object> data;
            try
            {
                data = REST.ParseREST(req.Body);
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if(!data.ContainsKey("METHOD"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
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
                        req.ErrorResponse(HttpStatusCode.BadRequest);
                        break;
                }
            }
            catch(FailureResultException)
            {
                using (HttpResponse res = req.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteStartElement("result");
                        writer.WriteValue("Failure");
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
            }
        }

        private void GetAvatar(HttpRequest req, Dictionary<string, object> reqdata)
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
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
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

        private void ResetAvatar(HttpRequest req, Dictionary<string, object> reqdata)
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

        private void RemoveItems(HttpRequest req, Dictionary<string, object> reqdata)
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

        private void SetAvatar(HttpRequest req, Dictionary<string, object> reqdata)
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

        private void SetItems(HttpRequest req, Dictionary<string, object> reqdata)
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
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection) =>
            new RobustAvatarServerHandler(ownSection.GetString("AvatarService", "AvatarService"));
    }
    #endregion
}
