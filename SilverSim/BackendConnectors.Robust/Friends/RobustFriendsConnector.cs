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
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.Types;
using SilverSim.Types.Friends;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Friends
{
    [Description("Robust Friends Connector")]
    [PluginName("Friends")]
    public sealed class RobustFriendsConnector : FriendsServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST FRIENDS CONNECTOR");

        private readonly string m_Uri;
        private string m_HomeUri;
        public int TimeoutMs = 20000;

        public RobustFriendsConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }

            m_Uri = ownSection.GetString("URI");
            if (!m_Uri.EndsWith("/"))
            {
                m_Uri += "/";
            }
            m_Uri += "friends";
            m_HomeUri = string.Empty;
        }

        public RobustFriendsConnector(string uri, string homeuri)
        {
            m_Uri = uri;
            if (!m_Uri.EndsWith("/"))
            {
                m_Uri += "/";
            }
            m_Uri += "friends";

            if(homeuri.Length != 0)
            {
                m_HomeUri = homeuri;
                if(!m_HomeUri.EndsWith("/"))
                {
                    m_HomeUri += "/";
                }
            }
            else
            {
                m_HomeUri = string.Empty;
            }
        }

        private void CheckResult(Map map)
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

        public override bool TryGetValue(UGUI user, UGUI friend, out FriendInfo fInfo)
        {
            foreach (FriendInfo fi in this[user])
            {
                if (fi.Friend.Equals(friend))
                {
                    fInfo = fi;
                    return true;
                }
            }
            fInfo = default(FriendInfo);
            return false;
        }

        public override FriendInfo this[UGUI user, UGUI friend]
        {
            get
            {
                FriendInfo fi;
                if(!TryGetValue(user, friend, out fi))
                {
                    throw new KeyNotFoundException();
                }
                return fi;
            }
        }

        public override List<FriendInfo> this[UGUI user]
        {
            get
            {
                var reslist = new List<FriendInfo>();
                var post = new Dictionary<string, string>
                {
                    ["METHOD"] = "getfriends_string",
                    ["PRINCIPALID"] = (string)user.ID
                };
                Map res;
                using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                {
                    res = OpenSimResponse.Deserialize(s);
                }
                foreach(KeyValuePair<string, IValue> kvp in res)
                {
                    if(kvp.Key.StartsWith("friend"))
                    {
                        var m = (Map)kvp.Value;
                        var fi = new FriendInfo
                        {
                            User = user
                        };
                        try
                        {
                            string friend = m["Friend"].ToString();
                            string[] parts = friend.Split(';');
                            if (parts.Length > 3)
                            {
                                /* fourth part is secret */
                                fi.Secret = parts[3];
                                fi.Friend = new UGUI(parts[0] + ";" + parts[1] + ";" + parts[2]);
                            }
                            else
                            {
                                fi.Friend = new UGUI(friend);
                            }

                            fi.UserGivenFlags = (FriendRightFlags)m["MyFlags"].AsInt;
                            fi.FriendGivenFlags = (FriendRightFlags)m["TheirFlags"].AsInt;
                        }
                        catch
                        {
                            /* no action needed */
                        }
                    }
                }
                return reslist;
            }
        }

        public override void Store(FriendInfo fi)
        {
            StoreEntry(fi.User, fi.Friend, fi.Secret, fi.UserGivenFlags);
            StoreEntry(fi.Friend, fi.User, fi.Secret, fi.FriendGivenFlags);
        }

        public override void Delete(FriendInfo fi)
        {
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "deletefriend_string",
                ["PRINCIPALID"] = fi.User.ToString(),
                ["FRIEND"] = fi.Friend.ToString()
            };
            if (fi.Friend.HomeURI != null)
            {
                post["FRIEND"] += ";" + fi.Secret;
            }

            using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* only called when initialized by ConfigurationLoader */
            m_HomeUri = loader.HomeURI;
        }

        public override void StoreRights(FriendInfo fi)
        {
            StoreEntry(fi.User, fi.Friend, fi.Secret, fi.FriendGivenFlags);
        }

        private void StoreEntry(UGUI user, UGUI friend, string secret, FriendRightFlags flags)
        {
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "storefriend",
                ["PrincipalID"] = (user.HomeURI != null && !user.HomeURI.ToString().StartsWith(m_HomeUri)) ?
                user + ";" + secret :
                user.ID.ToString(),

                ["Friend"] = (friend.HomeURI != null && !friend.HomeURI.ToString().StartsWith(m_HomeUri)) ?
                friend + ";" + secret :
                friend.ID.ToString(),

                ["MyFlags"] = ((int)flags).ToString()
            };
            using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void StoreOffer(FriendInfo fi)
        {
            StoreEntry(fi.Friend, fi.User, fi.Secret, 0);
        }
    }
}
