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
    public sealed class RobustFriendsConnector : FriendsServiceInterface, IPlugin
    {
        readonly string m_Uri;
        string m_HomeUri;
        public int TimeoutMs = 20000;

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

        public override bool TryGetValue(UUI user, UUI friend, out FriendInfo fInfo)
        {
            List<FriendInfo> filist = this[user];
            foreach (FriendInfo fi in filist)
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

        public override FriendInfo this[UUI user, UUI friend]
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

        public override List<FriendInfo> this[UUI user]
        {
            get 
            {
                List<FriendInfo> reslist = new List<FriendInfo>();
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["METHOD"] = "getfriends_string";
                post["PRINCIPALID"] = (string)user.ID;

                Map res;
                using(Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                {
                    res = OpenSimResponse.Deserialize(s);
                }
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "deletefriend_string";
            post["PRINCIPALID"] = fi.User.ToString();
            post["FRIEND"] = fi.Friend.ToString();
            if (fi.Friend.HomeURI != null)
            {
                post["FRIEND"] += ";" + fi.Secret;
            }

            using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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

        void StoreEntry(UUI user, UUI friend, string secret, FriendRightFlags flags)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "storefriend";
            post["PrincipalID"] = (user.HomeURI != null && !user.HomeURI.ToString().StartsWith(m_HomeUri)) ?
                user.ToString() + ";" + secret :
                user.ID.ToString();

            post["Friend"] = (friend.HomeURI != null && !friend.HomeURI.ToString().StartsWith(m_HomeUri)) ?
                friend.ToString() + ";" + secret :
                friend.ID.ToString();

            post["MyFlags"] = ((int)flags).ToString();

            using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }

        }

        public override void StoreOffer(FriendInfo fi)
        {
            StoreEntry(fi.Friend, fi.User, fi.Secret, 0);
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
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustFriendsConnector(ownSection.GetString("URI"), string.Empty);
        }
    }
    #endregion

}
