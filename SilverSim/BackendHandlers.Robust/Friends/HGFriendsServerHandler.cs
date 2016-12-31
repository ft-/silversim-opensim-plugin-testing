// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Types;
using SilverSim.Types.Friends;
using SilverSim.Types.Presence;
using SilverSim.Types.StructuredData.REST;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Friends
{
    [Description("Robust Friends Protocol Server")]
    public class HGFriendsServerHandler : IPlugin, IServiceURLsGetInterface
    {
        FriendsServiceInterface m_FriendsService;
        readonly string m_FriendsServiceName;
        BaseHttpServer m_HttpServer;
        readonly string m_PresenceServiceName;
        PresenceServiceInterface m_PresenceService;

        readonly Dictionary<string, Action<HttpRequest, Dictionary<string, object>>> m_Handlers = new Dictionary<string, Action<HttpRequest, Dictionary<string, object>>>();

        public HGFriendsServerHandler(IConfig ownSection)
        {
            m_FriendsServiceName = ownSection.GetString("FriendsService", "FriendsService");
            m_PresenceServiceName = ownSection.GetString("PresenceService", "PresenceService");
            m_Handlers.Add("statusnotification", HandleOnlineStatusNotification);
            m_Handlers.Add("deletefriendship", HandleDeleteFriendship);
        }

        public void GetServiceURLs(Dictionary<string, string> dict)
        {
            dict["FriendsServerURI"] = m_HttpServer.ServerURI;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_HttpServer = loader.HttpServer;
            m_FriendsService = loader.GetService<FriendsServiceInterface>(m_FriendsServiceName);
            m_PresenceService = loader.GetService<PresenceServiceInterface>(m_PresenceServiceName);
            m_HttpServer.UriHandlers.Add("/hgfriends", HGFriendsHandler);
            try
            {
                loader.HttpsServer.UriHandlers.Add("/hgfriends", HGFriendsHandler);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        string GetSecret(string friendId)
        {
            string[] parts = friendId.Split(Semicolon, 4);
            return (parts.Length == 4) ? parts[3] : string.Empty;
        }

        private static readonly char[] Semicolon = new char[1] { ';' };

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
                FailureResult(httpreq, e.Message);
            }
            catch
            {
                if (httpreq.Response != null)
                {
                    httpreq.Response.Close();
                }
                else
                {
                    FailureResult(httpreq);
                }
            }
        }

        void HandleDeleteFriendship(HttpRequest req, Dictionary<string, object> reqdata)
        {
            string secret;
            UUID userid;
            UUI friendid;
            object o;
            if(!reqdata.TryGetValue("SECRET", out o))
            {
                FailureResult(req);
                return;
            }
            secret = o.ToString();

            if(!reqdata.TryGetValue("PrincipalID", out o) || !UUID.TryParse(o.ToString(), out userid))
            {
                FailureResult(req);
                return;
            }

            if(!reqdata.TryGetValue("Friend", out o) || !UUI.TryParse(o.ToString(), out friendid))
            {
                FailureResult(req);
                return;
            }

            FriendInfo fi = new FriendInfo();
            fi.User.ID = userid;
            fi.Friend = friendid;
            fi.Secret = secret;

            try
            {
                m_FriendsService.Delete(fi);
                SuccessResult(req);
            }
            catch
            {
                FailureResult(req);
            }
        }

        void HandleOnlineStatusNotification(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID principalID = UUID.Zero;
            object o;
            if(!reqdata.TryGetValue("userID", out o) || UUID.TryParse(o.ToString(), out principalID))
            {
                FailureResult(req);
                return;
            }

            bool online;
            if(reqdata.TryGetValue("online", out o))
            {
                bool.TryParse(o.ToString(), out online);
            }
            else
            {
                FailureResult(req);
            }

            List<KeyValuePair<UUI, string>> friends = new List<KeyValuePair<UUI, string>>();
            foreach (KeyValuePair<string, object> kvp in reqdata)
            {
                if (kvp.Key.StartsWith("friend_"))
                {
                    UUI uui;
                    string val = kvp.Value.ToString();
                    if (UUI.TryParse(val, out uui))
                    {
                        friends.Add(new KeyValuePair<UUI, string>(uui, GetSecret(val)));
                    }
                }
            }

            List<UUID> onlineFriends = new List<UUID>();

            foreach(KeyValuePair<UUI, string> kvp in friends)
            {
                FriendInfo fi;
                if(m_FriendsService.TryGetValue(new UUI(principalID), kvp.Key, out fi) &&
                    fi.Secret == kvp.Value)
                {
                    List<PresenceInfo> pi = m_PresenceService[principalID];
                    if(pi.Count != 0)
                    {
                        onlineFriends.Add(kvp.Key.ID);
                    }

#warning TODO: Implement status notification sending
                }
            }

            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    if(onlineFriends.Count == 0)
                    {
                        writer.WriteNamedValue("RESULT", "NULL");
                    }
                    else
                    {
                        int i = 0;
                        foreach(UUID f in onlineFriends)
                        {
                            writer.WriteNamedValue("friend_" + i.ToString(), f.ToString());
                            ++i;
                        }
                    }
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
                    writer.WriteNamedValue("RESULT", "Success");
                    writer.WriteEndElement();
                }
            }
        }

        void FailureResult(HttpRequest req, string msg = "")
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteNamedValue("RESULT", "Failure");
                    writer.WriteNamedValue("Message", msg);
                    writer.WriteEndElement();
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
