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
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Types;
using SilverSim.Types.Presence;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Presence
{
    [Description("Robust Presence Connector")]
    [PluginName("Presence")]
    public class RobustPresenceConnector : PresenceServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST PRESENCE CONNECTOR");

        public int TimeoutMs { get; set; }
        private readonly string m_PresenceUri;

        #region Constructor
        public RobustPresenceConnector(string uri, string homeuri)
        {
            TimeoutMs = 20000;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "presence";
            m_PresenceUri = uri;
        }

        public RobustPresenceConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);

                throw new ConfigurationLoader.ConfigurationErrorException();
            }

            m_PresenceUri = ownSection.GetString("URI");
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        public override List<PresenceInfo> this[UUID userID]
        {
            get
            {
                var presences = new List<PresenceInfo>();
                var post = new Dictionary<string, string>
                {
                    ["uuids[]"] = (string)userID,
                    ["VERSIONMIN"] = "0",
                    ["VERSIONMAX"] = "0",
                    ["METHOD"] = "getagents"
                };
                Map map;
                using(Stream s = HttpClient.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }

                var m = map["result"] as Map;
                if (m == null)
                {
                    throw new PresenceNotFoundException();
                }
                foreach (IValue iv in m.Values)
                {
                    var pm = (Map)iv;
                    presences.Add(new PresenceInfo()
                    {
                        RegionID = pm["RegionID"].ToString(),
                        UserID = new UUI(pm["UserID"].ToString())
                    });
                }
                return presences;
            }
        }

        public override void Logout(UUID sessionID, UUID userID)
        {
            var post = new Dictionary<string, string>
            {
                ["VERSIONMIN"] = "0",
                ["VERSIONMAX"] = "0",
                ["SessionID"] = (string)sessionID,
                ["METHOD"] = "logout"
            };
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("result"))
            {
                throw new PresenceUpdateFailedException();
            }
            if (map["result"].ToString() != "Success")
            {
                throw new PresenceUpdateFailedException();
            }
        }

        public override PresenceInfo this[UUID sessionID, UUID userID]
        {
            get
            {
                var post = new Dictionary<string, string>
                {
                    ["SessionID"] = (string)sessionID,
                    ["VERSIONMIN"] = "0",
                    ["VERSIONMAX"] = "0",
                    ["METHOD"] = "getagent"
                };
                Map map;
                using(Stream s = HttpClient.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }
                var m = map["result"] as Map;
                if(m == null)
                {
                    throw new PresenceNotFoundException();
                }
                return new PresenceInfo()
                {
                    RegionID = m["RegionID"].ToString(),
                    UserID = new UUI(m["UserID"].ToString()),
                    SessionID = sessionID
                };
            }
        }

        public override void Login(PresenceInfo pInfo)
        {
            var post = new Dictionary<string, string>
            {
                ["VERSIONMIN"] = "0",
                ["VERSIONMAX"] = "0",
                ["UserID"] = (string)pInfo.UserID,
                ["SessionID"] = (string)pInfo.SessionID,
                ["SecureSessionID"] = (string)pInfo.SecureSessionID,
                ["METHOD"] = "login"
            };
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("result"))
            {
                throw new PresenceUpdateFailedException();
            }
            if (map["result"].ToString() != "Success")
            {
                throw new PresenceUpdateFailedException();
            }
        }

        public override void Report(PresenceInfo pInfo)
        {
            var post = new Dictionary<string, string>
            {
                ["VERSIONMIN"] = "0",
                ["VERSIONMAX"] = "0",
                ["METHOD"] = "report",
                ["SessionID"] = (string)pInfo.SessionID,
                ["RegionID"] = (string)pInfo.RegionID
            };
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("result"))
            {
                throw new PresenceUpdateFailedException();
            }
            if (map["result"].ToString() != "Success")
            {
                throw new PresenceUpdateFailedException();
            }
        }

        public override void LogoutRegion(UUID regionID)
        {
            var post = new Dictionary<string, string>
            {
                ["RegionID"] = (string)regionID,
                ["METHOD"] = "logoutregion"
            };
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("result"))
            {
                throw new PresenceLogoutRegionFailedException();
            }
            if (map["result"].ToString() != "Success")
            {
                throw new PresenceLogoutRegionFailedException();
            }
        }

        public override List<PresenceInfo> GetPresencesInRegion(UUID regionId)
        {
            throw new NotSupportedException("GetPresencesInRegion");
        }

        public override void Remove(UUID scopeID, UUID accountID)
        {
            throw new NotSupportedException("Remove");
        }
    }
}
