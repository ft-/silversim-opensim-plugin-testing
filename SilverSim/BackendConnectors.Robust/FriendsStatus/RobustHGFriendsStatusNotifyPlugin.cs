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

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.Threading;
using System;
using System.ComponentModel;

namespace SilverSim.BackendConnectors.Robust.FriendsStatus
{
    [PluginName("RobustHGFriendsPlugin")]
    [Description("OpenSim HGFriends Connector Factory")]
    public sealed class RobustHGFriendsStatusNotifyPlugin : ServicePluginHelo, IFriendsStatusNotifyServicePlugin, IPlugin
    {
        private readonly string m_LocalFriendsStatusNotifierName;
        private IFriendsStatusNotifyServiceInterface m_LocalFriendsStatusNotifier;
        private readonly RwLockedList<AvatarNameServiceInterface> m_AvatarNameServices = new RwLockedList<AvatarNameServiceInterface>();
        private readonly AggregatingAvatarNameService m_AvatarNameService;
        private readonly string[] m_AvatarNameServiceNames;

        public RobustHGFriendsStatusNotifyPlugin(IConfig config)
        {
            m_AvatarNameService = new AggregatingAvatarNameService(m_AvatarNameServices);
            m_LocalFriendsStatusNotifierName = config.GetString("LocalFriendsStatusNotifier", string.Empty);
            m_AvatarNameServiceNames = config.GetString("LocalAvatarNameServices", string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if(m_AvatarNameServiceNames.Length == 1 && string.IsNullOrEmpty(m_AvatarNameServiceNames[0]))
            {
                m_AvatarNameServiceNames = new string[0];
            }
        }

        public override string Name => "opensim-robust";

        public IFriendsStatusNotifyServiceInterface Instantiate(string url) => new RobustHGFriendsStatusNotifyService(url, m_LocalFriendsStatusNotifier, m_AvatarNameService);

        public void Startup(ConfigurationLoader loader)
        {
            if(!string.IsNullOrEmpty(m_LocalFriendsStatusNotifierName))
            {
                m_LocalFriendsStatusNotifier = loader.GetService<IFriendsStatusNotifyServiceInterface>(m_LocalFriendsStatusNotifierName);
            }

            foreach (string service in m_AvatarNameServiceNames)
            {
                m_AvatarNameServices.Add(loader.GetService<AvatarNameServiceInterface>(service.Trim()));
            }
        }
    }
}
