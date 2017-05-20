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
using SilverSim.ServiceInterfaces.Account;
using SilverSim.Types;
using SilverSim.Types.Account;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Account
{
    #region Service Implementation
    [Description("Robust UserAccount Connector")]
    public sealed class RobustAccountConnector : UserAccountServiceInterface, IPlugin
    {
        private readonly string m_UserAccountURI;
        public int TimeoutMs { get; set; }

        #region Constructor
        public RobustAccountConnector(string uri)
        {
            if(!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "accounts";
            m_UserAccountURI = uri;
            TimeoutMs = 20000;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        public override bool TryGetValue(UUID scopeID, UUID accountID, out UserAccount account)
        {
            var post = new Dictionary<string, string>
            {
                ["UserID"] = (string)accountID,
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "getaccount"
            };
            using (Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                var resultmap = map["result"] as Map;
                if (resultmap == null)
                {
                    account = default(UserAccount);
                    return false;
                }
                account = DeserializeEntry(resultmap);
                return true;
            }
        }

        public override bool ContainsKey(UUID scopeID, UUID accountID)
        {
            var post = new Dictionary<string, string>
            {
                ["UserID"] = (string)accountID,
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "getaccount"
            };
            using (Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                var resultmap = map["result"] as Map;
                return resultmap != null;
            }
        }

        public override UserAccount this[UUID scopeID, UUID accountID]
        {
            get
            {
                UserAccount account;
                if(!TryGetValue(scopeID, accountID, out account))
                {
                    throw new UserAccountNotFoundException();
                }
                return account;
            }
        }

        public override bool TryGetValue(UUID scopeID, string email, out UserAccount account)
        {
            var post = new Dictionary<string, string>
            {
                ["Email"] = email,
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "getaccount"
            };
            using (Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                if(!map.ContainsKey("result"))
                {
                    account = default(UserAccount);
                    return false;
                }
                var resultmap = map["result"] as Map;
                if (resultmap == null)
                {
                    account = default(UserAccount);
                    return false;
                }
                account = DeserializeEntry(resultmap);
                return true;
            }
        }

        public override bool ContainsKey(UUID scopeID, string email)
        {
            var post = new Dictionary<string, string>
            {
                ["Email"] = email,
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "getaccount"
            };
            using (Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                if (!map.ContainsKey("result"))
                {
                    return false;
                }
                Map resultmap = map["result"] as Map;
                return resultmap != null;
            }
        }

        public override UserAccount this[UUID scopeID, string email]
        {
            get
            {
                UserAccount account;
                if(!TryGetValue(scopeID, email, out account))
                {
                    throw new UserAccountNotFoundException();
                }
                return account;
            }
        }

        public override bool TryGetValue(UUID scopeID, string firstName, string lastName, out UserAccount account)
        {
            var post = new Dictionary<string, string>
            {
                ["FirstName"] = firstName,
                ["LastName"] = lastName,
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "getaccount"
            };
            using (Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                if(!map.ContainsKey("result"))
                {
                    account = default(UserAccount);
                    return false;
                }
                var resultmap = map["result"] as Map;
                if (resultmap == null)
                {
                    account = default(UserAccount);
                    return false;
                }
                account = DeserializeEntry(resultmap);
                return true;
            }
        }

        public override bool ContainsKey(UUID scopeID, string firstName, string lastName)
        {
            var post = new Dictionary<string, string>
            {
                ["FirstName"] = firstName,
                ["LastName"] = lastName,
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "getaccount"
            };
            using (Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                if (!map.ContainsKey("result"))
                {
                    return false;
                }
                var resultmap = map["result"] as Map;
                if (resultmap == null)
                {
                    return false;
                }
                return true;
            }
        }

        public override UserAccount this[UUID scopeID, string firstName, string lastName]
        {
            get
            {
                UserAccount account;
                if(!TryGetValue(scopeID, firstName, lastName, out account))
                {
                    throw new UserAccountNotFoundException();
                }
                return account;
            }
        }

        public override List<UserAccount> GetAccounts(UUID scopeID, string query)
        {
            var post = new Dictionary<string, string>
            {
                ["query"] = query,
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "getaccounts"
            };
            Map map;

            using(Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            var resultmap = map["result"] as Map;
            if (resultmap == null)
            {
                throw new UserAccountNotFoundException();
            }
            List<UserAccount> res = new List<UserAccount>();

            foreach (IValue i in resultmap.Values)
            {
                Map m = (Map)i;
                res.Add(DeserializeEntry(m));
            }
            return res;
        }

        private UserAccount DeserializeEntry(Map m)
        {
            UserAccount ua = new UserAccount();

            ua.Principal.FirstName = m["FirstName"].ToString();
            ua.Principal.LastName = m["LastName"].ToString();
            ua.Email = m["Email"].ToString();
            ua.Principal.ID = m["PrincipalID"].ToString();
            ua.ScopeID = m["ScopeID"].ToString();
            ua.Created = Date.UnixTimeToDateTime(m["Created"].AsULong);
            ua.UserLevel = int.Parse(m["UserLevel"].ToString());
            ua.UserFlags = uint.Parse(m["UserFlags"].ToString());
            ua.IsLocalToGrid = true;
            ua.IsEverLoggedIn = true;
            string serviceURLs = string.Empty;
            if(m.ContainsKey("ServiceURLs"))
            {
                serviceURLs = m["ServiceURLs"].ToString();
            }

            foreach(string p in serviceURLs.Split(';'))
            {
                string[] pa = p.Split(new char[] {'*'}, 2, StringSplitOptions.RemoveEmptyEntries);
                if(pa.Length < 2)
                {
                    continue;
                }
                ua.ServiceURLs[pa[0]] = pa[1];
            }
            return ua;
        }

        public override void Add(UserAccount userAccount)
        {
            throw new NotSupportedException();
        }

        public override void Update(UserAccount userAccount)
        {
            throw new NotSupportedException();
        }

        public override void Remove(UUID scopeID, UUID accountID)
        {
            throw new NotSupportedException();
        }

        public override void SetEverLoggedIn(UUID scopeID, UUID accountID)
        {
            /* intentionally left empty */
        }
    }
    #endregion

    #region Factory
    [PluginName("UserAccounts")]
    public sealed class RobustAccountConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST ACCOUNT CONNECTOR");

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustAccountConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
