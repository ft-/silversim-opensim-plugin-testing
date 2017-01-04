// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.AvatarName
{
    #region Service Implementation
    [Description("Robust UserAccount AvatarName Connector")]
    public sealed class RobustAccountAvatarNameConnector : AvatarNameServiceInterface, IPlugin
    {
        readonly string m_UserAccountURI;
        string m_HomeURI;
        readonly UUID m_ScopeID;
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
            /* only called when initialized by ConfigurationLoader */
            m_HomeURI = loader.HomeURI;
        }
        #endregion

        public override bool TryGetValue(string firstName, string lastName, out UUI uui)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["FirstName"] = firstName;
            post["LastName"] = lastName;
            post["SCOPEID"] = (string)m_ScopeID;
            post["METHOD"] = "getaccount";
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }

            if (!map.ContainsKey("result"))
            {
                uui = default(UUI);
                return false;
            }

            Map m = map["result"] as Map;
            if(null == m)
            {
                uui = default(UUI);
                return false;
            }
            uui = new UUI();
            uui.FirstName = m["FirstName"].ToString();
            uui.LastName = m["LastName"].ToString();
            uui.ID = m["PrincipalID"].ToString();
            uui.HomeURI = new Uri(m_HomeURI);
            uui.IsAuthoritative = true;
            return true;
        }

        public override UUI this[string firstName, string lastName] 
        { 
            get
            {
                UUI uui;
                if(!TryGetValue(firstName, lastName, out uui))
                {
                    throw new KeyNotFoundException();
                }
                return uui;
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
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }

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
                    /* no action needed */
                }
            }

            return results;
        }

        public override bool TryGetValue(UUID key, out UUI uui)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)key;
            post["SCOPEID"] = (string)m_ScopeID;
            post["METHOD"] = "getaccount";
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if(!map.ContainsKey("result"))
            {
                uui = default(UUI);
                return false;
            }
            Map m = map["result"] as Map;
            if (m == null)
            {
                uui = default(UUI);
                return false;
            }

            uui = new UUI();
            uui.FirstName = m["FirstName"].ToString();
            uui.LastName = m["LastName"].ToString();
            uui.ID = m["PrincipalID"].ToString();
            uui.HomeURI = new Uri(m_HomeURI);
            uui.IsAuthoritative = true;
            return true;
        }

        public override UUI this[UUID accountID]
        {
            get
            {
                UUI uui;
                if(!TryGetValue(accountID, out uui))
                {
                    throw new KeyNotFoundException();
                }
                return uui;
            }
        }

        public override void Store(UUI uui)
        {
            /* no action needed */
        }

        public override bool Remove(UUID key)
        {
            return false;
        }
    }
    #endregion

    #region Factory
    [PluginName("UserAccountAvatarNames")]
    public sealed class RobustAccountAvatarNameConnectorFactory : IPluginFactory
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
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustAccountAvatarNameConnector(ownSection.GetString("URI"), string.Empty, ownSection.GetString("ScopeID", (string)UUID.Zero));
        }
    }
    #endregion
}
