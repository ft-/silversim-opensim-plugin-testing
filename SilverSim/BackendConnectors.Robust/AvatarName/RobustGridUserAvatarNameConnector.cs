// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.Types;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.AvatarName
{
    #region Service Implementation
    class RobustGridUserAvatarNameConnector : AvatarNameServiceInterface, IPlugin
    {
        public int TimeoutMs { get; set; }
        string m_GridUserURI;

        #region Constructor
        public RobustGridUserAvatarNameConnector(string uri)
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

        private UUI fromResult(Map map)
        {
            UUI uui = new UUI(map["UserID"].ToString());
            uui.IsAuthoritative = null != uui.HomeURI;
            return uui;
        }

        public override UUI this[string firstName, string lastName] 
        { 
            get
            {
                throw new KeyNotFoundException();
            }
        }

        public override List<UUI> Search(string[] names)
        {
            return new List<UUI>();
        }

        public override UUI this[UUID userID]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["UserID"] = (string)userID;
                post["METHOD"] = "getgriduserinfo";
                Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs));
                if (!map.ContainsKey("result"))
                {
                    throw new KeyNotFoundException();
                }
                if (!(map["result"] is Map))
                {
                    throw new KeyNotFoundException();
                }
                return fromResult((Map)map["result"]);
            }
            set
            {

            }
        }
    }
    #endregion

    #region Factory
    [PluginName("GridUserAvatarNames")]
    public class RobustGridUserAvatarNameConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRIDUSER AVATAR NAME CONNECTOR");
        public RobustGridUserAvatarNameConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new RobustGridUserAvatarNameConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
