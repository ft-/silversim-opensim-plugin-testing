// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using System.ComponentModel;

namespace SilverSim.BackendHandlers.Robust.Friends
{
    #region Service Implementation
    [Description("Robust Friends Protocol Server")]
    public sealed class RobustFriendsServerHandler : IPlugin
    {
        //private static readonly ILog m_Log = LogManager.GetLogger("ROBUST FRIENDS HANDLER");
        public RobustFriendsServerHandler(string friends)
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
            /* intentionally left empty */
        }
    }
    #endregion

    #region Factory
    [PluginName("FriendsHandler")]
    public sealed class RobustFriendsHandlerFactory : IPluginFactory
    {
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
