// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.Types;
using SilverSim.Types.Friends;
using SilverSim.Types.StructuredData.REST;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Friends
{
    #region Service Implementation
    [Description("Robust Friends Protocol Server")]
    public sealed class RobustFriendsServerHandler : IPlugin
    {
        BaseHttpServer m_HttpServer;
        readonly string m_FriendsServiceName;
        readonly string m_UserAccountServiceName;

        UserAccountServiceInterface m_UserAccountService;
        FriendsServiceInterface m_FriendsService;

        readonly Dictionary<string, Action<HttpRequest, Dictionary<string, object>>> m_Handlers = new Dictionary<string, Action<HttpRequest, Dictionary<string, object>>>();

        //private static readonly ILog m_Log = LogManager.GetLogger("ROBUST FRIENDS HANDLER");
        public RobustFriendsServerHandler(IConfig ownSection)
        {
            m_FriendsServiceName = ownSection.GetString("FriendsService", "FriendsService");
            m_UserAccountServiceName = ownSection.GetString("UserAccountService", "UserAccountService");
            m_Handlers.Add("getfriends", GetFriendsHandler);
            m_Handlers.Add("getfriends_string", GetFriendsStringHandler);
            m_Handlers.Add("storefriend", StoreFriendHandler);
            m_Handlers.Add("deletefriend", DeleteFriendHandler);
            m_Handlers.Add("deletefriend_string", DeleteFriendStringHandler);
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
            m_FriendsService = loader.GetService<FriendsServiceInterface>(m_FriendsServiceName);
            m_HttpServer = loader.HttpServer;

            m_HttpServer.UriHandlers.Add("/friends", FriendsHandler);
            try
            {
                loader.HttpsServer.UriHandlers.Add("/friends", FriendsHandler);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        void FriendsHandler(HttpRequest httpreq)
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
                using (HttpResponse res = httpreq.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteNamedValue("Result", "Failure");
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
                            writer.WriteNamedValue("Result", "Failure");
                            writer.WriteNamedValue("Message", string.Empty);
                            writer.WriteEndElement();
                        }
                    }
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
                    writer.WriteNamedValue("Result", "Success");
                    writer.WriteEndElement();
                }
            }
        }

        void GetFriendsCommonHandler(HttpRequest req, List<FriendInfo> fis)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    if (fis.Count == 0)
                    {
                        writer.WriteNamedValue("result", "null");
                    }
                    else
                    {
                        int i = 0;
                        foreach (FriendInfo fi in fis)
                        {
                            writer.WriteStartElement("friend" + i.ToString());
                            writer.WriteAttributeString("type", "List");
                            writer.WriteNamedValue("PrincipalID", fi.User.ID);
                            writer.WriteNamedValue("Friend", fi.Friend.ToString());
                            writer.WriteNamedValue("MyFlags", ((uint)fi.UserGivenFlags).ToString());
                            writer.WriteNamedValue("TheirFlags", ((uint)fi.FriendGivenFlags).ToString());
                            writer.WriteEndElement();
                            ++i;
                        }
                    }
                    writer.WriteEndElement();
                }
            }
        }

        void GetFriendsHandler(HttpRequest req, Dictionary<string, object> reqdata)
        {
            object o;
            UUID id;
            if (!reqdata.TryGetValue("PRINCIPALID", out o) ||
                !UUID.TryParse(o.ToString(), out id))
            {
                throw new FailureResultException();
            }
            List<FriendInfo> fis = m_FriendsService[new UUI(id)];
            GetFriendsCommonHandler(req, fis);
        }

        void GetFriendsStringHandler(HttpRequest req, Dictionary<string, object> reqdata)
        {
            object o;
            UUI id;
            if (!reqdata.TryGetValue("PRINCIPALID", out o) ||
                !UUI.TryParse(o.ToString(), out id))
            {
                throw new FailureResultException();
            }
            List<FriendInfo> fis = m_FriendsService[id];
            GetFriendsCommonHandler(req, fis);
        }

        void StoreFriendHandler(HttpRequest req, Dictionary<string, object> reqdata)
        {
            FriendInfo fi = new FriendInfo();
            object o;
            if(!reqdata.TryGetValue("PRINCIPALID", out o) || !UUI.TryParse(o.ToString(), out fi.User))
            {
                throw new FailureResultException();
            }
            if(!reqdata.TryGetValue("Friend", out o) || !UUI.TryParse(o.ToString(), out fi.Friend))
            {
                throw new FailureResultException();
            }
            int flags;
            if(!reqdata.TryGetValue("MyFlags", out o) || !int.TryParse(o.ToString(), out flags))
            {
                throw new FailureResultException();
            }
            fi.FriendGivenFlags = (FriendRightFlags)flags;
            m_FriendsService.Store(fi);
            SuccessResult(req);
        }

        void DeleteFriendHandler(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID id;
            object o;
            if(!reqdata.TryGetValue("PRINCIPALID", out o) ||
                !UUID.TryParse(o.ToString(), out id))
            {
                throw new FailureResultException();
            }
            UUI friend;
            if(!reqdata.TryGetValue("FRIEND", out o) ||
                !UUI.TryParse(o.ToString(), out friend))
            {
                throw new FailureResultException();
            }

            FriendInfo fi = new FriendInfo();
            fi.User.ID = id;
            fi.Friend = friend;
            fi.Secret = GetSecret(o.ToString());

            FriendInfo exist_fi;
            if(!m_FriendsService.TryGetValue(fi.User, fi.Friend, out exist_fi) ||
                fi.Secret != exist_fi.Secret)
            {
                throw new FailureResultException();
            }
            m_FriendsService.Delete(fi);
            SuccessResult(req);
        }

        void DeleteFriendStringHandler(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI id;
            object o;
            if (!reqdata.TryGetValue("PRINCIPALID", out o) ||
                !UUI.TryParse(o.ToString(), out id))
            {
                throw new FailureResultException();
            }
            UUI friend;
            if (!reqdata.TryGetValue("FRIEND", out o) ||
                !UUI.TryParse(o.ToString(), out friend))
            {
                throw new FailureResultException();
            }

            string id_secret = GetSecret(o.ToString());
            string friend_secret = GetSecret(o.ToString());
            if(id_secret.Length != 0 && friend_secret.Length != 0)
            {
                throw new FailureResultException();
            }

            FriendInfo fi = new FriendInfo();
            fi.User = id;
            fi.Friend = friend;
            fi.Secret = friend_secret.Length != 0 ? friend_secret : id_secret;

            FriendInfo exist_fi;
            if (!m_FriendsService.TryGetValue(fi.User, fi.Friend, out exist_fi) ||
                fi.Secret != exist_fi.Secret)
            {
                throw new FailureResultException();
            }
            m_FriendsService.Delete(fi);
            SuccessResult(req);
        }

        string GetSecret(string friendId)
        {
            string[] parts = friendId.Split(Semicolon, 4);
            return (parts.Length == 4) ? parts[3] : string.Empty;
        }

        private static readonly char[] Semicolon = new char[1] { ';' };
    }
    #endregion

    #region Factory
    [PluginName("FriendsHandler")]
    public sealed class RobustFriendsHandlerFactory : IPluginFactory
    {
        public RobustFriendsHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustFriendsServerHandler(ownSection);
        }
    }
    #endregion
}
