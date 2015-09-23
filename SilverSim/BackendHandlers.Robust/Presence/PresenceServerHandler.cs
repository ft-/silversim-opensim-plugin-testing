// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilverSim.BackendHandlers.Robust.Presence
{
    #region Service Implementation
    class RobustPresenceServerHandler : IPlugin
    {
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
    public class RobustPresenceServerHandlerFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST INVENTORY HANDLER");
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
