// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.CmdIO;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Scene;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.OpenSimArchiver
{
    [Description("IAR Plugin")]
    public sealed class InventoryArchiverLoadStore : IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("IAR ARCHIVER");

        public InventoryArchiverLoadStore()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
        }
    }
}
