// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Profile;

namespace SilverSim.BackendConnectors.OpenSim
{
    public class ProfileInventoryPlugin : ServicePluginHelo, IProfileServicePlugin, IPlugin
    {
        string m_ProfileName;
        public ProfileInventoryPlugin(string profileName)
        {
            m_ProfileName = profileName;
        }

        public void Startup(ConfigurationLoader loader)
        {

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
            return new ProfileInventoryPlugin("opensim-robust");
        }
    }

}
