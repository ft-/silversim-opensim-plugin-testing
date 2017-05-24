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
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.AvatarName
{
    [Description("Robust UserAccount AvatarName Connector")]
    [PluginName("UserAccountAvatarNames")]
    public sealed class RobustAccountAvatarNameConnector : AvatarNameServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST ACCOUNT AVATAR NAME CONNECTOR");

        private readonly string m_UserAccountURI;
        private string m_HomeURI;
        private readonly UUID m_ScopeID;
        public int TimeoutMs { get; set; }

        #region Constructor
        public RobustAccountAvatarNameConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            string uri = ownSection.GetString("URI");
            m_ScopeID = ownSection.GetString("ScopeID", (string)UUID.Zero);
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "accounts";
            m_UserAccountURI = uri;
            TimeoutMs = 20000;
            m_HomeURI = string.Empty;
        }

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
            var post = new Dictionary<string, string>
            {
                ["FirstName"] = firstName,
                ["LastName"] = lastName,
                ["SCOPEID"] = (string)m_ScopeID,
                ["METHOD"] = "getaccount"
            };
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

            var m = map["result"] as Map;
            if(m == null)
            {
                uui = default(UUI);
                return false;
            }
            uui = new UUI()
            {
                FirstName = m["FirstName"].ToString(),
                LastName = m["LastName"].ToString(),
                ID = m["PrincipalID"].ToString(),
                HomeURI = new Uri(m_HomeURI),
                IsAuthoritative = true
            };
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
            var post = new Dictionary<string, string>
            {
                ["VERSIONMIN"] = "0",
                ["VERSIONMAX"] = "0",
                ["query"] = string.Join(" ", names),
                ["ScopeID"] = (string)m_ScopeID,
                ["METHOD"] = "getaccounts"
            };
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_UserAccountURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }

            var results = new List<UUI>();

            foreach(IValue iv in map.Values)
            {
                try
                {
                    var m = iv as Map;
                    results.Add(new UUI()
                    {
                        FirstName = m["FirstName"].ToString(),
                        LastName = m["LastName"].ToString(),
                        ID = m["PrincipalID"].ToString(),
                        HomeURI = new Uri(m_HomeURI),
                        IsAuthoritative = true
                    });
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
            var post = new Dictionary<string, string>
            {
                ["UserID"] = (string)key,
                ["SCOPEID"] = (string)m_ScopeID,
                ["METHOD"] = "getaccount"
            };
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
            var m = map["result"] as Map;
            if (m == null)
            {
                uui = default(UUI);
                return false;
            }

            uui = new UUI()
            {
                FirstName = m["FirstName"].ToString(),
                LastName = m["LastName"].ToString(),
                ID = m["PrincipalID"].ToString(),
                HomeURI = new Uri(m_HomeURI),
                IsAuthoritative = true
            };
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

        public override bool Remove(UUID key) => false;
    }
}
