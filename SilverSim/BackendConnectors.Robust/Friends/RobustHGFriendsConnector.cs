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
        private readonly string m_Uri;
        public int TimeoutMs = 20000;
        private readonly UUID m_SessionID;
        private readonly string m_ServiceKey;

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
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "getfriendperms",
                ["PRINCIPALID"] = (string)user.ID,
                ["FRIENDID"] = (string)friend.ID,
                ["KEY"] = m_ServiceKey,
                ["SESSIONID"] = (string)m_SessionID
            };
            Map res;
            using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }
            if (res.ContainsKey("Value") && res["Value"] != null)
            {
                fInfo = new FriendInfo
                {
                    User = user,
                    Friend = friend,
                    UserGivenFlags = (FriendRightFlags)res["Value"].AsInt
                };
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

        public override List<FriendInfo> this[UUI user] => new List<FriendInfo>();

        public override void Store(FriendInfo fi)
        {
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "newfriendship",
                ["KEY"] = m_ServiceKey,
                ["SESSIONID"] = (string)m_SessionID,
                ["PrincipalID"] = (string)fi.User.ID,
                ["Friend"] = fi.Friend.ToString(),
                ["SECRET"] = fi.Secret,
                ["MyFlags"] = ((int)fi.UserGivenFlags).ToString(),
                ["TheirFlags"] = ((int)fi.FriendGivenFlags).ToString()
            };
            using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void Delete(FriendInfo fi)
        {
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "deletefriendship",
                ["PrincipalID"] = (string)fi.User.ID,
                ["Friend"] = fi.Friend.ToString(),
                ["SECRET"] = fi.Secret,
                ["MyFlags"] = "0",
                ["TheirFlags"] = "0"
            };
            using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
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
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "grant_rights",
                ["FromID"] = (string)fi.User.ID,
                ["ToID"] = fi.Friend.ToString(),
                ["SECRET"] = fi.Secret,
                ["UserFlags"] = "-1",
                ["Rights"] = ((int)fi.UserGivenFlags).ToString()
            };
            using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
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
