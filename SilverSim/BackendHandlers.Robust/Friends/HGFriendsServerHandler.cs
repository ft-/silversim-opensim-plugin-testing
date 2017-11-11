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
using SilverSim.BackendConnectors.Robust.Friends;
using SilverSim.BackendConnectors.Robust.UserAgent;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.ServiceInterfaces.Traveling;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Friends;
using SilverSim.Types.Presence;
using SilverSim.Types.ServerURIs;
using SilverSim.Types.StructuredData.REST;
using SilverSim.Types.TravelingData;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Friends
{
    [Description("Robust Friends Protocol Server")]
    [PluginName("HGFriendsHandler")]
    public class HGFriendsServerHandler : IPlugin, IServiceURLsGetInterface
    {
        private FriendsServiceInterface m_FriendsService;
        private readonly string m_FriendsServiceName;
        private IFriendsStatusNotifyServiceInterface m_FriendsStatusNotifier;
        private readonly string m_FriendsStatusNotifierName;
        private BaseHttpServer m_HttpServer;
        private readonly string m_PresenceServiceName;
        private PresenceServiceInterface m_PresenceService;
        private UserAccountServiceInterface m_UserAccountService;
        private readonly string m_UserAccountServiceName;
        private AvatarNameServiceInterface m_AvatarNameService;
        private readonly string m_TravelingDataServiceName;
        private TravelingDataServiceInterface m_TravelingDataService;
        private RwLockedList<AvatarNameServiceInterface> m_AvatarNameServices = new RwLockedList<AvatarNameServiceInterface>();
        private readonly string[] m_AvatarNameServiceNames;

        private readonly Dictionary<string, Action<HttpRequest, Dictionary<string, object>>> m_Handlers = new Dictionary<string, Action<HttpRequest, Dictionary<string, object>>>();

        public HGFriendsServerHandler(IConfig ownSection)
        {
            m_UserAccountServiceName = ownSection.GetString("UserAccountService", "UserAccountService");
            m_FriendsServiceName = ownSection.GetString("FriendsService", "FriendsService");
            m_PresenceServiceName = ownSection.GetString("PresenceService", "PresenceService");
            m_FriendsStatusNotifierName = ownSection.GetString("FriendsStatusNotifier", string.Empty);
            m_TravelingDataServiceName = ownSection.GetString("TravelingDataService", "TravelingDataService");
            m_AvatarNameServiceNames = ownSection.GetString("AvatarNameServices", string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if(m_AvatarNameServiceNames.Length == 1 && string.IsNullOrEmpty(m_AvatarNameServiceNames[0]))
            {
                m_AvatarNameServiceNames = new string[0];
            }
            m_AvatarNameService = new AggregatingAvatarNameService(m_AvatarNameServices);
            m_Handlers.Add("statusnotification", HandleOnlineStatusNotification);
            m_Handlers.Add("deletefriendship", HandleDeleteFriendship);
            m_Handlers.Add("friendship_offered", HandleFriendshipOffered);
            m_Handlers.Add("newfriendship", HandleNewFriendship);
            m_Handlers.Add("validate_friendship_offered", HandleValidateFriendshipOffered);
            m_Handlers.Add("getfriendperms", HandleGetFriendPerms);
            m_Handlers.Add("grant_rights", HandleGrantRights);
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
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
            m_TravelingDataService = loader.GetService<TravelingDataServiceInterface>(m_TravelingDataServiceName);
            if (!string.IsNullOrEmpty(m_FriendsStatusNotifierName))
            {
                m_FriendsStatusNotifier = loader.GetService<IFriendsStatusNotifyServiceInterface>(m_FriendsStatusNotifierName);
            }
            m_HttpServer.UriHandlers.Add("/hgfriends", HGFriendsHandler);
            BaseHttpServer https;
            if(loader.TryGetHttpsServer(out https))
            {
                https.UriHandlers.Add("/hgfriends", HGFriendsHandler);
            }

            foreach (string service in m_AvatarNameServiceNames)
            {
                m_AvatarNameServices.Add(loader.GetService<AvatarNameServiceInterface>(service.Trim()));
            }
        }

        private string GetSecret(string friendId)
        {
            string[] parts = friendId.Split(Semicolon, 4);
            return (parts.Length == 4) ? parts[3] : string.Empty;
        }

        private static readonly char[] Semicolon = new char[1] { ';' };

        private void HGFriendsHandler(HttpRequest httpreq)
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

        private void HandleGrantRights(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI user;
            UUI friend;
            object o;
            string secret;
            uint flags;

            if (!reqdata.TryGetValue("PrincipalID", out o) || !UUI.TryParse(o.ToString(), out user) ||
                !reqdata.TryGetValue("Friend", out o) || !UUI.TryParse(o.ToString(), out friend) ||
                !reqdata.TryGetValue("Rights", out o) || !uint.TryParse(o.ToString(), out flags))
            {
                FailureResult(req);
                return;
            }

            if(!reqdata.TryGetValue("SECRET", out o))
            {
                FailureResult(req);
                return;
            }

            secret = o.ToString();

            FriendInfo finfo;
            if(m_FriendsService.TryGetValue(user, friend, out finfo))
            {
                finfo.UserGivenFlags = (FriendRightFlags)flags;
                m_FriendsService.StoreRights(finfo);
                SuccessResult(req);
            }
            else
            {
                FailureResult(req);
            }
        }

        private void HandleGetFriendPerms(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI user;
            UUI friend;
            UUID sessionid;
            object o;
            if(!reqdata.TryGetValue("PrincipalID", out o) || !UUI.TryParse(o.ToString(), out user) ||
                !reqdata.TryGetValue("Friend", out o) || !UUI.TryParse(o.ToString(), out friend) ||
                !reqdata.TryGetValue("SESSIONID", out o) || !UUID.TryParse(o.ToString(), out sessionid))
            {
                FailureResult(req);
                return;
            }

            TravelingDataInfo travelingData;
            try
            {
                travelingData = m_TravelingDataService.GetTravelingData(sessionid);
            }
            catch
            {
                FailureResult(req);
                return;
            }

            if(travelingData.UserID != user.ID)
            {
                FailureResult(req);
                return;
            }

            FriendInfo finfo;
            if(m_FriendsService.TryGetValue(user, friend, out finfo))
            {
                using (HttpResponse res = req.BeginResponse("text/xml"))
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteNamedValue("RESULT", "Success");
                    writer.WriteNamedValue("Value", ((uint)finfo.FriendGivenFlags).ToString());
                    writer.WriteEndElement();
                }

            }
            else
            {
                FailureResult(req);
            }
        }

        private void HandleNewFriendship(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID sessionID;
            object o;

            UUID userID;
            UUI friendID;
            string secret;

            if (!reqdata.TryGetValue("PrincipalID", out o) || !UUID.TryParse(o.ToString(), out userID))
            {
                FailureResult(req);
                return;
            }
            if (!reqdata.TryGetValue("Friend", out o) || !TryParseUUIWithSecret(o.ToString(), out friendID, out secret))
            {
                FailureResult(req);
                return;
            }

            if (!m_UserAccountService.ContainsKey(UUID.Zero, userID))
            {
                FailureResult(req);
                return;
            }

            FriendInfo finfo;
            FriendInfo ofinfo;
            if (m_FriendsService.TryGetValue(friendID, new UUI(userID), out finfo) &&
                !m_FriendsService.TryGetValue(new UUI(userID), friendID, out ofinfo))
            {
                /* valid offer entry, skip the major validation */
                if (secret == finfo.Secret)
                {
                    finfo.UserGivenFlags = FriendRightFlags.SeeOnline;
                    finfo.FriendGivenFlags = FriendRightFlags.SeeOnline;
                    m_FriendsService.Store(finfo);
                    SuccessResult(req);
                }
                else
                {
                    FailureResult(req);
                }
                return;
            }

            if (!reqdata.TryGetValue("SESSIONID", out o) || !UUID.TryParse(o.ToString(), out sessionID))
            {
                FailureResult(req);
                return;
            }

            TravelingDataInfo travelingInfo;

            try
            {
                travelingInfo = m_TravelingDataService.GetTravelingData(sessionID);
            }
            catch
            {
                FailureResult(req);
                return;
            }
            
            if(travelingInfo.UserID != userID)
            {
                FailureResult(req);
                return;
            }

            if(friendID.HomeURI == null)
            {
                FailureResult(req);
                return;
            }

            var userAgentConn = new RobustUserAgentConnector(friendID.HomeURI.ToString());

            UUI lookupid;
            try
            {
                lookupid = userAgentConn.GetUUI(friendID, friendID);
            }
            catch
            {
                FailureResult(req);
                return;
            }

            if(!lookupid.EqualsGrid(friendID))
            {
                FailureResult(req);
                return;
            }

            if (!m_AvatarNameService.TryGetValue(friendID, out lookupid))
            {
                m_AvatarNameService.Store(friendID);
            }
            else if (!lookupid.EqualsGrid(friendID))
            {
                FailureResult(req);
                return;
            }

            m_FriendsService.StoreOffer(new FriendInfo { User = new UUI(userID), Friend = friendID, Secret = secret });
            /* TODO: forward to sim */
            SuccessResult(req);
        }

        private bool TryParseUUIWithSecret(string input, out UUI friend, out string secret)
        {
            string[] parts = input.Split(';');
            if (parts.Length > 3)
            {
                /* fourth part is secret */
                secret = parts[3];
                return UUI.TryParse(parts[0] + ";" + parts[1] + ";" + parts[2], out friend);
            }
            else
            {
                secret = string.Empty;
                return UUI.TryParse(input, out friend);
            }
        }

        private void HandleValidateFriendshipOffered(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID userID;
            UUID friendID;
            object o;
            if(!reqdata.TryGetValue("PrincipalID", out o) || !UUID.TryParse(o.ToString(), out userID))
            {
                FailureResult(req);
                return;
            }
            if(!reqdata.TryGetValue("Friend", out o) || !UUID.TryParse(o.ToString(), out friendID))
            {
                FailureResult(req);
                return;
            }
            UUI user;
            UUI friend;
            if(!m_AvatarNameService.TryGetValue(userID, out user) || 
                !m_AvatarNameService.TryGetValue(friendID, out friend))
            {
                FailureResult(req);
                return;
            }
            FriendInfo finfo;
            if(m_FriendsService.TryGetValue(user, friend, out finfo))
            {
                SuccessResult(req);
            }
            else
            {
                FailureResult(req);
            }
        }

        private void HandleFriendshipOffered(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI friendid;
            UUID toId;
            try
            {
                friendid = new UUI(UUID.Parse(reqdata["FromID"].ToString()))
                {
                    FullName = reqdata["FromName"].ToString()
                };
                toId = UUID.Parse(reqdata["ToID"].ToString());
            }
            catch
            {
                FailureResult(req);
                return;
            }

            if(!m_UserAccountService.ContainsKey(UUID.Zero, toId) || friendid.HomeURI == null)
            {
                FailureResult(req);
                return;
            }

            UUI foundid;
            if(m_AvatarNameService.TryGetValue(friendid.ID, out foundid) && !foundid.EqualsGrid(friendid))
            {
                FailureResult(req);
            }

            var userAgentConn = new RobustUserAgentConnector(friendid.HomeURI.ToString());
            UUI lookupid;
            try
            {
                lookupid = userAgentConn.GetUUI(friendid, friendid);
            }
            catch
            {
                FailureResult(req);
                return;
            }

            if(!lookupid.EqualsGrid(friendid))
            {
                FailureResult(req);
                return;
            }

            ServerURIs serveruris = userAgentConn.GetServerURLs(friendid);

            string friendsServerURI;
            try
            {
                friendsServerURI = serveruris.FriendsServerURI;
            }
            catch
            {
                FailureResult(req);
                return;
            }

            var friendsConn = new RobustHGFriendsConnector(friendsServerURI, UUID.Zero, string.Empty);
            if(!friendsConn.ValidateFriendshipOffered(new UUI(toId), friendid))
            {
                FailureResult(req);
                return;
            }
            
            /* TODO: forward to simulator */
            SuccessResult(req);
        }

        private void HandleDeleteFriendship(HttpRequest req, Dictionary<string, object> reqdata)
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

            var fi = new FriendInfo
            {
                User = new UUI(userid),
                Friend = friendid,
                Secret = secret
            };
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

        private void HandleOnlineStatusNotification(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID principalID = UUID.Zero;
            object o;
            UUI principal;
            if(!reqdata.TryGetValue("userID", out o) || !UUID.TryParse(o.ToString(), out principalID) || !m_AvatarNameService.TryGetValue(principalID, out principal))
            {
                FailureResult(req);
                return;
            }

            bool online = false;
            if(reqdata.TryGetValue("online", out o))
            {
                bool.TryParse(o.ToString(), out online);
            }
            else
            {
                FailureResult(req);
            }

            var friends = new List<KeyValuePair<UUI, string>>();
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

            Action<UUI, List<KeyValuePair<UUI, string>>> notifyFriends;
            if(online)
            {
                notifyFriends = m_FriendsStatusNotifier.NotifyAsOnline;
            }
            else
            {
                notifyFriends = m_FriendsStatusNotifier.NotifyAsOffline;
            }

            var onlineFriends = new List<UUID>();

            foreach(KeyValuePair<UUI, string> kvp in friends)
            {
                FriendInfo fi;
                if(m_FriendsService.TryGetValue(principal, kvp.Key, out fi) &&
                    fi.Secret == kvp.Value)
                {
                    List<PresenceInfo> pi = m_PresenceService[principalID];
                    if (pi.Count != 0 && (fi.UserGivenFlags & FriendRightFlags.SeeOnline) != 0)
                    {
                        onlineFriends.Add(kvp.Key.ID);
                    }

                    notifyFriends(principal, new List<KeyValuePair<UUI, string>> { kvp });
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

        private void SuccessResult(HttpRequest req)
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

        private void FailureResult(HttpRequest req, string msg = "")
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
}
