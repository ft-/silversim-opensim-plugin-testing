// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.GridUser;
using SilverSim.Types;
using SilverSim.Types.GridUser;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.GridUser
{
    #region Service Implementation
    public class RobustGridUserConnector : GridUserServiceInterface, IPlugin
    {
        public int TimeoutMs { get; set; }
        string m_GridUserURI;

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

        }
        #endregion

        private GridUserInfo fromResult(Map map)
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

        private GridUserInfo GetUserInfo(string userID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = userID;
            post["METHOD"] = "getgriduserinfo";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs));
            if (!map.ContainsKey("result"))
            {
                throw new GridUserNotFoundException();
            }
            if (!(map["result"] is Map))
            {
                throw new GridUserNotFoundException();
            }
            return fromResult((Map)map["result"]);
        }

        public override GridUserInfo this[UUID userID]
        {
            get
            {
                return GetUserInfo((string)userID);
            }
        }
        public override GridUserInfo this[UUI userID]
        {
            get
            {
                return GetUserInfo((string)userID.ID);
            }
        }

        private void checkResult(Map map)
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
            checkResult(OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs)));
        }

        public override void LoggedOut(UUI userID, UUID lastRegionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)userID;
            post["RegionID"] = lastRegionID.ToString();
            post["Position"] = lastPosition.ToString();
            post["LookAt"] = lastLookAt.ToString();
            post["METHOD"] = "loggedout";
            checkResult(OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs)));
        }

        public override void SetHome(UUI userID, UUID homeRegionID, Vector3 homePosition, Vector3 homeLookAt)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)userID;
            post["RegionID"] = homeRegionID.ToString();
            post["Position"] = homePosition.ToString();
            post["LookAt"] = homeLookAt.ToString();
            post["METHOD"] = "sethome";
            checkResult(OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs)));
        }

        public override void SetPosition(UUI userID, UUID lastRegionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)userID;
            post["RegionID"] = lastRegionID.ToString();
            post["Position"] = lastPosition.ToString();
            post["LookAt"] = lastLookAt.ToString();
            post["METHOD"] = "setposition";
            checkResult(OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs)));
        }
    }
    #endregion

    #region Factory
    [PluginName("GridUser")]
    public class RobustGridUserConnectorFactory : IPluginFactory
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
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new RobustGridUserConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
