// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;

namespace SilverSim.BackendHandlers.Robust.Groups
{
    #region Service Implementation
    class RobustGroupsServerHandler : IPlugin
    {
        public RobustGroupsServerHandler(string groupsServiceName)
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
        }
    }
    #endregion

    #region Factory
    [PluginName("GroupsHandler")]
    public class RobustGroupsServerHandlerFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GROUPS HANDLER");
        public RobustGroupsServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustGroupsServerHandler(ownSection.GetString("GroupsService", "GroupsService"));
        }
    }
    #endregion
}
