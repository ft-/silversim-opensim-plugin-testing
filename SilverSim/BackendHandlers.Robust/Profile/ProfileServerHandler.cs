// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.Profile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SilverSim.BackendHandlers.Robust.Profile
{
    [Description("Robust CoreProfile/OpenSimProfile Protocol Server")]
    public class ProfileServerHandler : IPlugin
    {
        ProfileServiceInterface m_ProfileService;
        UserAccountServiceInterface m_UserAccountService;
        readonly string m_ProfileServiceName;
        readonly string m_UserAccountServiceName;

        public ProfileServerHandler(IConfig ownSection)
        {
            m_ProfileServiceName = ownSection.GetString("ProfileService", "ProfileService");
            m_UserAccountServiceName = ownSection.GetString("UserAccountService", "UserAccountService");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_ProfileService = loader.GetService<ProfileServiceInterface>(m_ProfileServiceName);
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
        }
    }

    [PluginName("ProfileHandler")]
    public class ProfileServerHandlerFactory : IPluginFactory
    {
        public ProfileServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new ProfileServerHandler(ownSection);
        }
    }
}
