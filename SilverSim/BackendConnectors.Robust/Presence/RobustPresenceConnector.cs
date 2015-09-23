// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Types;
using SilverSim.Types.Presence;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.Presence
{
    #region Service Implementation
    public class RobustPresenceConnector : PresenceServiceInterface, IPlugin
    {
        public int TimeoutMs { get; set; }
        string m_PresenceUri;
        string m_HomeURI;

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
            m_HomeURI = homeuri;
        }

        public void Startup(ConfigurationLoader loader)
        {
        }
        #endregion

        public override List<PresenceInfo> this[UUID userID]
        {
            get
            {
                List<PresenceInfo> presences = new List<PresenceInfo>();
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["uuids[]"] = (string)userID;
                post["VERSIONMIN"] = "0";
                post["VERSIONMAX"] = "0";
                post["METHOD"] = "getagents";

                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs));
                if (!(map["result"] is Map))
                {
                    throw new PresenceNotFoundException();
                }
                Map m = (Map)(map["result"]);
                foreach (IValue iv in m.Values)
                {
                    PresenceInfo p = new PresenceInfo();
                    Map pm = (Map)iv;
                    p.RegionID = pm["RegionID"].ToString();
                    p.UserID.ID = pm["UserID"].ToString();
                    presences.Add(p);
                }
                return presences;
            }
        }

        public override PresenceInfo this[UUID sessionID, UUID userID]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["SessionID"] = (string)sessionID;
                post["VERSIONMIN"] = "0";
                post["VERSIONMAX"] = "0";
                post["METHOD"] = "getagent";

                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs));
                if(!(map["result"] is Map))
                {
                    throw new PresenceNotFoundException();
                }
                PresenceInfo p = new PresenceInfo();
                Map m = (Map)(map["result"]);
                p.RegionID = m["RegionID"].ToString();
                p.UserID.ID = m["UserID"].ToString();
                p.SessionID = sessionID;
                return p;
            }
            set
            {
                if(value == null)
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["VERSIONMIN"] = "0";
                    post["VERSIONMAX"] = "0";
                    post["SessionID"] = (string)sessionID;
                    post["METHOD"] = "logout";

                    Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs));
                    if (!map.ContainsKey("result"))
                    {
                        throw new PresenceUpdateFailedException();
                    }
                    if (map["result"].ToString() != "Success")
                    {
                        throw new PresenceUpdateFailedException();
                    }
                }
                else
                {
                    throw new ArgumentException("setting value != null is not allowed without reportType");
                }
            }
        }

        public override PresenceInfo this[UUID sessionID, UUID userID, SetType reportType]
        {
            set
            {
                if (value == null)
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["VERSIONMIN"] = "0";
                    post["VERSIONMAX"] = "0";
                    post["SessionID"] = (string)sessionID;
                    post["METHOD"] = "logout";

                    Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs));
                    if (!map.ContainsKey("result"))
                    {
                        throw new PresenceUpdateFailedException();
                    }
                    if (map["result"].ToString() != "Success")
                    {
                        throw new PresenceUpdateFailedException();
                    }
                }
                else if(reportType == SetType.Login)
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["VERSIONMIN"] = "0";
                    post["VERSIONMAX"] = "0";
                    post["UserID"] = (string)value.UserID;
                    post["SessionID"] = (string)value.SessionID;
                    post["SecureSessionID"] = (string)value.SecureSessionID;
                    post["METHOD"] = "login";

                    Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs));
                    if (!map.ContainsKey("result"))
                    {
                        throw new PresenceUpdateFailedException();
                    }
                    if (map["result"].ToString() != "Success")
                    {
                        throw new PresenceUpdateFailedException();
                    }
                }
                else if(reportType == SetType.Report)
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["VERSIONMIN"] = "0";
                    post["VERSIONMAX"] = "0";
                    post["METHOD"] = "report";
                    post["SessionID"] = (string)value.SessionID;
                    post["RegionID"] = (string)value.RegionID;

                    Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs));
                    if (!map.ContainsKey("result"))
                    {
                        throw new PresenceUpdateFailedException();
                    }
                    if (map["result"].ToString() != "Success")
                    {
                        throw new PresenceUpdateFailedException();
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid reportType specified");
                }
            }
        }

        public override void logoutRegion(UUID regionID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RegionID"] = (string)regionID;
            post["METHOD"] = "logoutregion";

            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_PresenceUri, null, post, false, TimeoutMs));
            if (!map.ContainsKey("result"))
            {
                throw new PresenceLogoutRegionFailedException();
            }
            if (map["result"].ToString() != "Success")
            {
                throw new PresenceLogoutRegionFailedException();
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("Presence")]
    public class RobustPresenceConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST PRESENCE CONNECTOR");
        public RobustPresenceConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI") || !ownSection.Contains("HomeURI"))
            {
                if (!ownSection.Contains("URI"))
                {
                    m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                }

                if (!ownSection.Contains("HomeURI"))
                {
                    m_Log.FatalFormat("Missing 'HomeURI' in section {0}", ownSection.Name);
                }

                throw new ConfigurationLoader.ConfigurationError();
            }

            string homeURI = ownSection.GetString("HomeURI");
            if(!Uri.IsWellFormedUriString(homeURI, UriKind.Absolute))
            {
                m_Log.FatalFormat("Invalid 'HomeURI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationError();
            }

            return new RobustPresenceConnector(ownSection.GetString("URI"), homeURI);
        }
    }
    #endregion
}
