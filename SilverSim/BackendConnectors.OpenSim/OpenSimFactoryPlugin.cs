// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Profile;
using System.ComponentModel;

namespace SilverSim.BackendConnectors.OpenSim
{
    [Description("OpenSim Profile Connector Factory")]
    public class OpenSimProfilePlugin : ServicePluginHelo, IProfileServicePlugin, IPlugin
    {
        readonly string m_ProfileName;
        public OpenSimProfilePlugin(string profileName)
        {
            m_ProfileName = profileName;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public ProfileServiceInterface Instantiate(string url)
        {
            return new Profile.ProfileConnector(url);
        }

        public override string Name
        {
            get
            {
                return m_ProfileName;
            }
        }
    }

    [PluginName("RobustProfilePlugin")]
    public class RobustProfilePluginFactory : IPluginFactory
    {
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new OpenSimProfilePlugin("opensim-robust");
        }
    }

}
