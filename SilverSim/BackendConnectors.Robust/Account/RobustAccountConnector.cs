// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.Types;
using SilverSim.Types.Account;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.Account
{
    #region Service Implementation
    class RobustAccountConnector : UserAccountServiceInterface, IPlugin
    {
        string m_UserAccountURI;
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

        }
        #endregion

        public override UserAccount this[UUID scopeID, UUID accountID]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["UserID"] = (string)accountID;
                post["SCOPEID"] = (string)scopeID;
                post["METHOD"] = "getaccount";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs));
                if (!(map["result"] is Map))
                {
                    throw new UserAccountNotFoundException();
                }
                return DeserializeEntry((Map)(map["result"]));
            }
        }

        public override UserAccount this[UUID scopeID, string email]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["Email"] = email;
                post["SCOPEID"] = (string)scopeID;
                post["METHOD"] = "getaccount";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs));
                if (!(map["result"] is Map))
                {
                    throw new UserAccountNotFoundException();
                }
                return DeserializeEntry((Map)(map["result"]));
            }
        }

        public override UserAccount this[UUID scopeID, string firstName, string lastName]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["FirstName"] = firstName;
                post["LastName"] = lastName;
                post["SCOPEID"] = (string)scopeID;
                post["METHOD"] = "getaccount";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs));
                if (!(map["result"] is Map))
                {
                    throw new UserAccountNotFoundException();
                }
                return DeserializeEntry((Map)(map["result"]));
            }
        }

        public override List<UserAccount> GetAccounts(UUID scopeID, string query)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["query"] = query;
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "getaccounts";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs));
            if (!(map["result"] is Map))
            {
                throw new UserAccountNotFoundException();
            }
            List<UserAccount> res = new List<UserAccount>();

            foreach(IValue i in ((Map)map["result"]).Values)
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
            ua.UserFlags = int.Parse(m["UserFlags"].ToString());
            ua.IsLocalToGrid = true;
            string serviceURLs = "";
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
    public class RobustAccountConnectorFactory : IPluginFactory
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
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new RobustAccountConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
