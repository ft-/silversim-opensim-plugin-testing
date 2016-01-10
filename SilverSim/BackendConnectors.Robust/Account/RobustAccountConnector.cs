// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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
        readonly string m_UserAccountURI;
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)accountID;
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "getaccount";
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                Map resultmap = map["result"] as Map;
                if (null == resultmap)
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)accountID;
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "getaccount";
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                Map resultmap = map["result"] as Map;
                return null != resultmap;
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["Email"] = email;
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "getaccount";
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                if(!map.ContainsKey("result"))
                {
                    account = default(UserAccount);
                    return false;
                }
                Map resultmap = map["result"] as Map;
                if (null == resultmap)
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["Email"] = email;
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "getaccount";
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                if (!map.ContainsKey("result"))
                {
                    return false;
                }
                Map resultmap = map["result"] as Map;
                return null != resultmap;
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["FirstName"] = firstName;
            post["LastName"] = lastName;
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "getaccount";
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                if(!map.ContainsKey("result"))
                {
                    account = default(UserAccount);
                    return false;
                }
                Map resultmap = map["result"] as Map;
                if (null == resultmap)
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["FirstName"] = firstName;
            post["LastName"] = lastName;
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "getaccount";
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                Map map = OpenSimResponse.Deserialize(s);
                if (!map.ContainsKey("result"))
                {
                    return false;
                }
                Map resultmap = map["result"] as Map;
                if (null == resultmap)
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["query"] = query;
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "getaccounts";
            Map map;
            
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            Map resultmap = map["result"] as Map;
            if (null == resultmap)
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
    }
    #endregion

    #region Factory
    [PluginName("UserAccounts")]
    public sealed class RobustAccountConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST ACCOUNT CONNECTOR");
        public RobustAccountConnectorFactory()
        {

        }

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
