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
using SilverSim.ServiceInterfaces.GridUser;
using SilverSim.Types;
using SilverSim.Types.GridUser;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GridUser
{
    [Description("Robust GridUser Connector")]
    [PluginName("GridUser")]
    public sealed class RobustGridUserConnector : GridUserServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRIDUSER CONNECTOR");

        public int TimeoutMs { get; set; }
        private readonly string m_GridUserURI;

        #region Constructor
        public RobustGridUserConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            string uri = ownSection.GetString("URI");

            TimeoutMs = 20000;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "griduser";

            m_GridUserURI = uri;
        }

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
            var info = new GridUserInfo()
            {
                User = new UUI(map["UserID"].ToString()),
                HomeRegionID = map["HomeRegionID"].ToString(),
                HomePosition = Vector3.Parse(map["HomePosition"].ToString()),
                HomeLookAt = Vector3.Parse(map["HomeLookAt"].ToString()),
                LastRegionID = map["LastRegionID"].ToString(),
                LastPosition = Vector3.Parse(map["LastPosition"].ToString()),
                LastLookAt = Vector3.Parse(map["LastLookAt"].ToString()),
                IsOnline = map["Online"].AsBoolean
            };
            DateTime login;
            DateTime logout;
            DateTime.TryParse(map["Login"].ToString(), out login);
            DateTime.TryParse(map["Logout"].ToString(), out logout);
            info.LastLogin = new Date(login);
            info.LastLogout = new Date(logout);
            return info;
        }

        private bool TryGetUserInfo(string userID, out GridUserInfo gridUserInfo)
        {
            var post = new Dictionary<string, string>
            {
                ["UserID"] = userID,
                ["METHOD"] = "getgriduserinfo"
            };
            Map map;
            using (Stream s = new HttpClient.Post(m_GridUserURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("result"))
            {
                gridUserInfo = default(GridUserInfo);
                return false;
            }
            var resultmap = map["result"] as Map;
            if (resultmap == null)
            {
                gridUserInfo = default(GridUserInfo);
                return false;
            }
            gridUserInfo = FromResult(resultmap);
            return true;
        }

        public override bool TryGetValue(UUID userID, out GridUserInfo userInfo) =>
            TryGetUserInfo((string)userID, out userInfo);

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

        public override bool TryGetValue(UUI userID, out GridUserInfo userInfo) =>
            TryGetUserInfo((string)userID, out userInfo);

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
            var post = new Dictionary<string, string>
            {
                ["UserID"] = (string)userID,
                ["METHOD"] = "loggedin"
            };
            using (Stream s = new HttpClient.Post(m_GridUserURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void LoggedOut(UUI userID, UUID lastRegionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            var post = new Dictionary<string, string>
            {
                ["UserID"] = (string)userID,
                ["RegionID"] = lastRegionID.ToString(),
                ["Position"] = lastPosition.ToString(),
                ["LookAt"] = lastLookAt.ToString(),
                ["METHOD"] = "loggedout"
            };
            using (Stream s = new HttpClient.Post(m_GridUserURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void SetHome(UUI userID, UUID homeRegionID, Vector3 homePosition, Vector3 homeLookAt)
        {
            var post = new Dictionary<string, string>
            {
                ["UserID"] = (string)userID,
                ["RegionID"] = homeRegionID.ToString(),
                ["Position"] = homePosition.ToString(),
                ["LookAt"] = homeLookAt.ToString(),
                ["METHOD"] = "sethome"
            };
            using (Stream s = new HttpClient.Post(m_GridUserURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void SetPosition(UUI userID, UUID lastRegionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            var post = new Dictionary<string, string>
            {
                ["UserID"] = (string)userID,
                ["RegionID"] = lastRegionID.ToString(),
                ["Position"] = lastPosition.ToString(),
                ["LookAt"] = lastLookAt.ToString(),
                ["METHOD"] = "setposition"
            };
            using (Stream s = new HttpClient.Post(m_GridUserURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }
    }
}
