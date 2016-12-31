// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.Types;
using SilverSim.Types.Friends;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System;

namespace SilverSim.BackendConnectors.Robust.Friends
{
    [Description("Robust HGFriends Connector")]
    public class RobustHGFriendsConnector : FriendsServiceInterface, IPlugin
    {
        readonly string m_Uri;
        public int TimeoutMs = 20000;
        readonly UUID m_SessionID;
        readonly string m_ServiceKey;

        public RobustHGFriendsConnector(string uri, UUID sessionID, string serviceKey)
        {
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "hgfriends";
            m_Uri = uri;
            m_SessionID = sessionID;
            m_ServiceKey = serviceKey;
        }

        private void CheckResult(Map map)
        {
            if (!map.ContainsKey("RESULT"))
            {
                throw new FriendUpdateFailedException();
            }
            if (map["RESULT"].ToString().ToLower() != "true")
            {
                throw new FriendUpdateFailedException();
            }
        }

        public override bool TryGetValue(UUI user, UUI friend, out FriendInfo fInfo)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "getfriendperms";
            post["PRINCIPALID"] = (string)user.ID;
            post["FRIENDID"] = (string)friend.ID;
            post["KEY"] = m_ServiceKey;
            post["SESSIONID"] = (string)m_SessionID;

            Map res;
            using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
            {
                res = OpenSimResponse.Deserialize(s);
            }
            if (res.ContainsKey("Value") && res["Value"] != null)
            {
                fInfo = new FriendInfo();
                fInfo.User = user;
                fInfo.Friend = friend;
                fInfo.UserGivenFlags = (FriendRightFlags)res["Value"].AsInt;
                return true;
            }
            fInfo = default(FriendInfo);
            return false;
        }

        public override FriendInfo this[UUI user, UUI friend]
        {
            get 
            {
                FriendInfo fi;
                if (!TryGetValue(user, friend, out fi))
                {
                    throw new KeyNotFoundException();
                }
                return fi;
            }
        }

        public override List<FriendInfo> this[UUI user]
        {
            get
            {
                return new List<FriendInfo>();
            }
        }

        public override void Store(FriendInfo fi)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "newfriendship";
            post["KEY"] = m_ServiceKey;
            post["SESSIONID"] = (string)m_SessionID;
            post["PrincipalID"] = (string)fi.User.ID;
            post["Friend"] = fi.Friend.ToString();
            post["SECRET"] = fi.Secret;
            post["MyFlags"] = ((int)fi.UserGivenFlags).ToString();
            post["TheirFlags"] = ((int)fi.FriendGivenFlags).ToString();

            using(Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void Delete(FriendInfo fi)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "deletefriendship";
            post["PrincipalID"] = (string)fi.User.ID;
            post["Friend"] = fi.Friend.ToString();
            post["SECRET"] = fi.Secret;
            post["MyFlags"] = "0";
            post["TheirFlags"] = "0";

            using(Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public override void StoreRights(FriendInfo fi)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "grant_rights";
            post["FromID"] = (string)fi.User.ID;
            post["ToID"] = fi.Friend.ToString();
            post["SECRET"] = fi.Secret;
            post["UserFlags"] = "-1";
            post["Rights"] = ((int)fi.UserGivenFlags).ToString();

            using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void StoreOffer(FriendInfo fi)
        {
            throw new NotImplementedException();
        }
    }
}
