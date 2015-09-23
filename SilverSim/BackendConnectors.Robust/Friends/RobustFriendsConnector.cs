// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.Types;
using SilverSim.Types.Friends;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.Friends
{
    class RobustFriendsConnector : FriendsServiceInterface, IPlugin
    {
        string m_Uri;
        public int TimeoutMs = 20000;

        public RobustFriendsConnector(string uri)
        {
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "friends";
            m_Uri = uri;
        }

        private void checkResult(Map map)
        {
            if (!map.ContainsKey("Result"))
            {
                throw new FriendUpdateFailedException();
            }
            if (map["Result"].ToString().ToLower() != "success")
            {
                throw new FriendUpdateFailedException();
            }
        }

        public override FriendInfo this[UUI user, UUI friend]
        {
            get 
            {
                List<FriendInfo> filist = this[user];
                foreach(FriendInfo fi in filist)
                {
                    if(fi.Friend.Equals(friend))
                    {
                        return fi;
                    }
                }
                throw new KeyNotFoundException();
            }
        }

        public override List<FriendInfo> this[UUI user]
        {
            get 
            {
                List<FriendInfo> reslist = new List<FriendInfo>();
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["METHOD"] = "getfriends_string";
                post["PRINCIPALID"] = (string)user.ID;

                Map res = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs));
                foreach(KeyValuePair<string, IValue> kvp in res)
                {
                    if(kvp.Key.StartsWith("friend"))
                    {
                        Map m = (Map)kvp.Value;
                        FriendInfo fi = new FriendInfo();
                        fi.User = user;
                        try
                        {
                            string friend = m["Friend"].ToString();
                            string[] parts = friend.Split(';');
                            if (parts.Length > 3)
                            {
                                /* fourth part is secret */
                                fi.Secret = parts[3];
                                fi.Friend.FullName = parts[0] + ";" + parts[1] + ";" + parts[2];
                            }
                            else
                            {
                                fi.Friend.FullName = friend;
                            }

                            fi.UserGivenFlags = m["MyFlags"].AsInt;
                            fi.FriendGivenFlags = m["TheirFlags"].AsInt;
                        }
                        catch
                        {

                        }
                    }
                }
                return reslist;
            }
        }

        public override void Store(FriendInfo fi)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "storefriend";
            post["PrincipalID"] = fi.User.ToString();
            post["Friend"] = fi.Friend.ToString();
            if(fi.Friend.HomeURI != null)
            {
                post["Friend"] += ";" + fi.Secret;
            }
            post["MyFlags"] = fi.UserGivenFlags.ToString();

            checkResult(OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs)));
        }

        public override void Delete(FriendInfo fi)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "deletefriend_string";
            post["PRINCIPALID"] = fi.User.ToString();
            post["FRIEND"] = fi.Friend.ToString();
            if (fi.Friend.HomeURI != null)
            {
                post["FRIEND"] += ";" + fi.Secret;
            }

            checkResult(OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs)));
        }

        public void Startup(ConfigurationLoader loader)
        {
        }
    }

    #region Factory
    [PluginName("Friends")]
    public class RobustFriendsConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST FRIENDS CONNECTOR");
        public RobustFriendsConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new RobustFriendsConnector(ownSection.GetString("URI"));
        }
    }
    #endregion

}
