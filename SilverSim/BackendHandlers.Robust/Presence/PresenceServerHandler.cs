// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using System.ComponentModel;

namespace SilverSim.BackendHandlers.Robust.Presence
{
    #region Service Implementation
    [Description("Robust Presence Protocol Server")]
    public sealed class RobustPresenceServerHandler : IPlugin
    {
        //private static readonly ILog m_Log = LogManager.GetLogger("ROBUST PRESENCE HANDLER");
        public RobustPresenceServerHandler(string presenceServiceName)
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
        }
    }
    #endregion

    #region Factory
    [PluginName("PresenceHandler")]
    public sealed class RobustPresenceServerHandlerFactory : IPluginFactory
    {
        public RobustPresenceServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustPresenceServerHandler(ownSection.GetString("PresenceService", "PresenceService"));
        }
    }
    #endregion
}
