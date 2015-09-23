// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.Types;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.AvatarName
{
    #region Service Implementation
    class RobustAccountAvatarNameConnector : AvatarNameServiceInterface, IPlugin
    {
        string m_UserAccountURI;
        string m_HomeURI;
        UUID m_ScopeID;
        public int TimeoutMs { get; set; }

        #region Constructor
        public RobustAccountAvatarNameConnector(string uri, string homeURI, UUID scopeID)
        {
            m_ScopeID = scopeID;
            if(!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "accounts";
            m_UserAccountURI = uri;
            TimeoutMs = 20000;
            m_HomeURI = homeURI;
        }

        public void Startup(ConfigurationLoader loader)
        {

        }
        #endregion

        public override UUI this[string firstName, string lastName] 
        { 
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["FirstName"] = firstName;
                post["LastName"] = lastName;
                post["SCOPEID"] = (string)m_ScopeID;
                post["METHOD"] = "getaccount";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs));
                if (!(map["result"] is Map))
                {
                    throw new KeyNotFoundException();
                }

                Map m = (Map)(map["result"]);
                UUI nd = new UUI();
                nd.FirstName = m["FirstName"].ToString();
                nd.LastName = m["LastName"].ToString();
                nd.ID = m["PrincipalID"].ToString();
                nd.HomeURI = new Uri(m_HomeURI);
                nd.IsAuthoritative = true;
                return nd;
            }
        }

        public override List<UUI> Search(string[] names)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["VERSIONMIN"] = "0";
            post["VERSIONMAX"] = "0";
            post["query"] = string.Join(" ", names);
            post["ScopeID"] = (string)m_ScopeID;
            post["METHOD"] = "getaccounts";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs));

            List<UUI> results = new List<UUI>();

            foreach(IValue iv in map.Values)
            {
                try
                {
                    Map m = iv as Map;
                    UUI nd = new UUI();
                    nd.FirstName = m["FirstName"].ToString();
                    nd.LastName = m["LastName"].ToString();
                    nd.ID = m["PrincipalID"].ToString();
                    nd.HomeURI = new Uri(m_HomeURI);
                    nd.IsAuthoritative = true;
                    results.Add(nd);
                }
                catch
                {

                }
            }

            return results;
        }

        public override UUI this[UUID accountID]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["UserID"] = (string)accountID;
                post["SCOPEID"] = (string)m_ScopeID;
                post["METHOD"] = "getaccount";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs));
                if (!(map["result"] is Map))
                {
                    throw new KeyNotFoundException();
                }

                Map m = (Map)(map["result"]);
                UUI nd = new UUI();
                nd.FirstName = m["FirstName"].ToString();
                nd.LastName = m["LastName"].ToString();
                nd.ID = m["PrincipalID"].ToString();
                nd.HomeURI = new Uri(m_HomeURI);
                nd.IsAuthoritative = true;
                return nd;
            }
            set
            {

            }
        }
    }
    #endregion

    #region Factory
    [PluginName("UserAccountAvatarNames")]
    public class RobustAccountAvatarNameConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST ACCOUNT AVATAR NAME CONNECTOR");
        public RobustAccountAvatarNameConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationError();
            }
            if (!ownSection.Contains("HomeURI"))
            {
                m_Log.FatalFormat("Missing 'HomeURI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new RobustAccountAvatarNameConnector(ownSection.GetString("URI"), ownSection.GetString("HomeURI"), ownSection.GetString("ScopeID", (string)UUID.Zero));
        }
    }
    #endregion
}
