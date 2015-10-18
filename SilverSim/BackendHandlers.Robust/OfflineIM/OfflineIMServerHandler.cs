﻿// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilverSim.BackendHandlers.Robust.OfflineIM
{
    #region Service Implementation
    public sealed class RobustOfflineIMServerHandler : IPlugin
    {
        //private static readonly ILog m_Log = LogManager.GetLogger("ROBUST INVENTORY HANDLER");
        public RobustOfflineIMServerHandler(string offlineIMServiceName)
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
        }
    }
    #endregion

    #region Factory
    [PluginName("OfflineIMHandler")]
    public sealed class RobustOfflineIMServerHandlerFactory : IPluginFactory
    {
        public RobustOfflineIMServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustOfflineIMServerHandler(ownSection.GetString("OfflineIMService", "OfflineIMService"));
        }
    }
    #endregion
}
