// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.Types;
using SilverSim.Types.Friends;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilverSim.BackendConnectors.Robust.Friends
{
    public class RobustHGFriendsConnector : FriendsServiceInterface, IPlugin
    {
        string m_Uri;
        public int TimeoutMs = 20000;
        UUID m_SessionID;
        string m_ServiceKey;

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

        private void checkResult(Map map)
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


        public override FriendInfo this[UUI user, UUI friend]
        {
            get 
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["METHOD"] = "getfriendperms";
                post["PRINCIPALID"] = (string)user.ID;
                post["FRIENDID"] = (string)friend.ID;
                post["KEY"] = m_ServiceKey;
                post["SESSIONID"] = (string)m_SessionID;

                Map res = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs));
                if(res.ContainsKey("Value") && res["Value"] != null)
                {
                    FriendInfo fi = new FriendInfo();
                    fi.User = user;
                    fi.Friend = friend;
                    fi.UserGivenFlags = res["Value"].AsInt;
                    return fi;
                }
                throw new KeyNotFoundException();
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
            post["MyFlags"] = fi.UserGivenFlags.ToString();
            post["TheirFlags"] = fi.FriendGivenFlags.ToString();

            checkResult(OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs)));
        }

        public override void Delete(FriendInfo fi)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "deletefriendship";
            post["PrincipalID"] = (string)fi.User.ID;
            post["Friend"] = fi.Friend.ToString();
            post["SECRET"] = fi.Secret;
            post["MyFlags"] = fi.UserGivenFlags.ToString();
            post["TheirFlags"] = fi.FriendGivenFlags.ToString();

            checkResult(OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs)));
        }

        public void Startup(ConfigurationLoader loader)
        {
        }
    }
}
