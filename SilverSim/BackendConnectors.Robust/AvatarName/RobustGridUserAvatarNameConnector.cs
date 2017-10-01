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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.AvatarName
{
    [Description("Robust GridUser AvatarName Connector")]
    [PluginName("GridUserAvatarNames")]
    public sealed class RobustGridUserAvatarNameConnector : AvatarNameServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRIDUSER AVATAR NAME CONNECTOR");

        public int TimeoutMs { get; set; }
        readonly string m_GridUserURI;

        #region Constructor
        public RobustGridUserAvatarNameConnector(IConfig ownSection)
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

        public override bool TryGetValue(string firstName, string lastName, out UUI uui)
        {
            uui = default(UUI);
            return false;
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

        public override bool TryGetValue(UUID userID, out UUI uui)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)userID;
            post["METHOD"] = "getgriduserinfo";
            Map map;
            using (Stream s = new HttpClient.Post(m_GridUserURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("result"))
            {
                uui = default(UUI);
                return false;
            }
            Map m = map["result"] as Map;
            if (null == m)
            {
                uui = default(UUI);
                return false;
            }
            uui = FromResult(m);
            return true;
        }

        public override UUI this[UUID userID]
        {
            get
            {
                UUI uui;
                if(!TryGetValue(userID, out uui))
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
}
