// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;

namespace SilverSim.OpenSimArchiver
{
    [PluginName("InventoryArchiveSupport")]
    public class InventoryArchiverLoadStoreFactory : IPluginFactory
    {
        public InventoryArchiverLoadStoreFactory()
        {

        }
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new InventoryArchiverLoadStore(ownSection);
        }
    }

    [PluginName("NpcInventoryArchiveSupport")]
    public class NpcInventoryArchiverLoadStoreFactory : IPluginFactory
    {
        public NpcInventoryArchiverLoadStoreFactory()
        {

        }
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new NpcInventoryArchiverLoadStore(ownSection);
        }
    }

    [PluginName("RegionArchiveSupport")]
    public class OpenSimArchiverLoadStoreFactory : IPluginFactory
    {
        public OpenSimArchiverLoadStoreFactory()
        {

        }
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new OpenSimArchiverLoadStore();
        }
    }
    [PluginName("OSAssetsArchiveSupport")]
    public class OSAssetsLoadFactory : IPluginFactory
    {
        public OSAssetsLoadFactory()
        {

        }
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new OSAssetsLoad();
        }
    }
}
