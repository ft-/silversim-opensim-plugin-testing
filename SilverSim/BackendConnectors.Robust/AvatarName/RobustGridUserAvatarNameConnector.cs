// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.Types;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.AvatarName
{
    #region Service Implementation
    public sealed class RobustGridUserAvatarNameConnector : AvatarNameServiceInterface, IPlugin
    {
        public int TimeoutMs { get; set; }
        readonly string m_GridUserURI;

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
            /* no action needed */
        }
        #endregion

        private UUI FromResult(Map map)
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
                Map map;
                using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridUserURI, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }
                if (!map.ContainsKey("result"))
                {
                    throw new KeyNotFoundException();
                }
                if (!(map["result"] is Map))
                {
                    throw new KeyNotFoundException();
                }
                return FromResult((Map)map["result"]);
            }
            set
            {
                /* no action needed */
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("GridUserAvatarNames")]
    public sealed class RobustGridUserAvatarNameConnectorFactory : IPluginFactory
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
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustGridUserAvatarNameConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
