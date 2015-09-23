// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;

namespace SilverSim.OpenSimArchiver
{
    [PluginName("OpenSimArchiveSupport")]
    public class ArchiverLoadStoreFactory : IPluginFactory
    {
        public ArchiverLoadStoreFactory()
        {

        }
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new ArchiverLoadStore();
        }
    }
}
