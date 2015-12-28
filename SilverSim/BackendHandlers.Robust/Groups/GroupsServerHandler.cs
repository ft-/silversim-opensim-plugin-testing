// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using System.ComponentModel;

namespace SilverSim.BackendHandlers.Robust.Groups
{
    #region Service Implementation
    [Description("Robust Groups Protocol Server")]
    public sealed class RobustGroupsServerHandler : IPlugin
    {
        //private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GROUPS HANDLER");
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
    public sealed class RobustGroupsServerHandlerFactory : IPluginFactory
    {
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
