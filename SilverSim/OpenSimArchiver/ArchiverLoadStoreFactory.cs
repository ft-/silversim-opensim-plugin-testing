// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common;

namespace SilverSim.OpenSimArchiver
{
    [PluginName("OpenSimArchiveSupport")]
    public class ArchiverLoadStoreFactory : IPluginFactory
    {
        public ArchiverLoadStoreFactory()
        {

        }
        public IPlugin Initialize(ConfigurationLoader loader, Nini.Config.IConfig ownSection)
        {
            return new ArchiverLoadStore();
        }
    }
}
