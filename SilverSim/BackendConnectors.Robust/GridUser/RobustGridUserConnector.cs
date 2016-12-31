// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.GridUser;
using SilverSim.Types;
using SilverSim.Types.GridUser;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GridUser
{
    #region Service Implementation
    [Description("Robust GridUser Connector")]
    public sealed class RobustGridUserConnector : GridUserServiceInterface, IPlugin
    {
        public int TimeoutMs { get; set; }
        readonly string m_GridUserURI;

        #region Constructor
        public RobustGridUserConnector(string uri)
        {
            TimeoutMs = 20000;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "griduser";

            m_GridUserURI = uri;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        private GridUserInfo FromResult(Map map)
        {
            GridUserInfo info = new GridUserInfo();
            info.User = new UUI(map["UserID"].ToString());
            info.HomeRegionID = map["HomeRegionID"].ToString();
            info.HomePosition = Vector3.Parse(map["HomePosition"].ToString());
            info.HomeLookAt = Vector3.Parse(map["HomeLookAt"].ToString());
            info.LastRegionID = map["LastRegionID"].ToString();
            info.LastPosition = Vector3.Parse(map["LastPosition"].ToString());
            info.LastLookAt = Vector3.Parse(map["LastLookAt"].ToString());
            info.IsOnline = map["Online"].AsBoolean;
            DateTime login;
            DateTime logout;
            DateTime.TryParse(map["Login"].ToString(), out login);
            DateTime.TryParse(map["Logout"].ToString(), out logout);
            info.LastLogin = new Date(login);
            info.LastLogout = new Date(logout);
            return info;
        }

        bool TryGetUserInfo(string userID, out GridUserInfo gridUserInfo)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = userID;
            post["METHOD"] = "getgriduserinfo";
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("result"))
            {
                gridUserInfo = default(GridUserInfo);
                return false;
            }
            Map resultmap = map["result"] as Map;
            if (null == resultmap)
            {
                gridUserInfo = default(GridUserInfo);
                return false;
            }
            gridUserInfo = FromResult(resultmap);
            return true;
        }

        public override bool TryGetValue(UUID userID, out GridUserInfo userInfo)
        {
            return TryGetUserInfo((string)userID, out userInfo);
        }

        public override GridUserInfo this[UUID userID]
        {
            get
            {
                GridUserInfo gridUserInfo;
                if(TryGetUserInfo((string)userID, out gridUserInfo))
                {
                    throw new GridUserNotFoundException();
                }
                return gridUserInfo;
            }
        }

        public override bool TryGetValue(UUI userID, out GridUserInfo userInfo)
        {
            return TryGetUserInfo((string)userID, out userInfo);
        }

        public override GridUserInfo this[UUI userID]
        {
            get
            {
                GridUserInfo gridUserInfo;
                if (TryGetUserInfo((string)userID, out gridUserInfo))
                {
                    throw new GridUserNotFoundException();
                }
                return gridUserInfo;
            }
        }

        private void CheckResult(Map map)
        {
            if (!map.ContainsKey("result"))
            {
                throw new GridUserUpdateFailedException();
            }
            if (!map["result"].AsBoolean)
            {
                throw new GridUserUpdateFailedException();
            }
        }

        public override void LoggedInAdd(UUI userID)
        {
            throw new NotSupportedException();
        }

        public override void LoggedIn(UUI userID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)userID;
            post["METHOD"] = "loggedin";
            using(Stream s = HttpClient.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void LoggedOut(UUI userID, UUID lastRegionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)userID;
            post["RegionID"] = lastRegionID.ToString();
            post["Position"] = lastPosition.ToString();
            post["LookAt"] = lastLookAt.ToString();
            post["METHOD"] = "loggedout";
            using (Stream s = HttpClient.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void SetHome(UUI userID, UUID homeRegionID, Vector3 homePosition, Vector3 homeLookAt)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)userID;
            post["RegionID"] = homeRegionID.ToString();
            post["Position"] = homePosition.ToString();
            post["LookAt"] = homeLookAt.ToString();
            post["METHOD"] = "sethome";
            using (Stream s = HttpClient.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void SetPosition(UUI userID, UUID lastRegionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)userID;
            post["RegionID"] = lastRegionID.ToString();
            post["Position"] = lastPosition.ToString();
            post["LookAt"] = lastLookAt.ToString();
            post["METHOD"] = "setposition";
            using (Stream s = HttpClient.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("GridUser")]
    public sealed class RobustGridUserConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRIDUSER CONNECTOR");
        public RobustGridUserConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustGridUserConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
