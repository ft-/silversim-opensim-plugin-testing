// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;

namespace SilverSim.BackendHandlers.Robust.Grid
{
    #region Service Implementation
    class RobustGridServerHandler : IPlugin
    {
        public RobustGridServerHandler(string gridServiceName)
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
        }
    }
    #endregion

    #region Factory
    [PluginName("GridHandler")]
    public class RobustGridHandlerFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRID HANDLER");
        public RobustGridHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustGridServerHandler(ownSection.GetString("GridService", "GridService"));
        }
    }
    #endregion
}
