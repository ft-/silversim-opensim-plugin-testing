// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;

namespace SilverSim.BackendHandlers.Robust.Friends
{
    #region Service Implementation
    class RobustFriendsServerHandler : IPlugin
    {
        public RobustFriendsServerHandler(string friends)
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
        }
    }
    #endregion

    #region Factory
    [PluginName("FriendsHandler")]
    public class RobustFriendsHandlerFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST FRIENDS HANDLER");
        public RobustFriendsHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustFriendsServerHandler(ownSection.GetString("FriendsService", "FriendsService"));
        }
    }
    #endregion
}
