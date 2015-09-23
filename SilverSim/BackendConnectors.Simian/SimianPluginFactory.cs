// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;

namespace SilverSim.BackendConnectors.Simian
{
    public class SimianInventoryPlugin : ServicePluginHelo, IInventoryServicePlugin, IPlugin
    {
        public SimianInventoryPlugin()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {

        }

        public InventoryServiceInterface Instantiate(string url)
        {
            return new Inventory.SimianInventoryConnector(url, (string)UUID.Zero);
        }

        public override string Name
        {
            get
            {
                return "opensim-simian";
            }
        }
    }
    [PluginName("InventoryPlugin")]
    public class SimianInventoryPluginFactory : IPluginFactory
    {
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new SimianInventoryPlugin();
        }
    }

    public class SimianAssetPlugin : ServicePluginHelo, IAssetServicePlugin, IPlugin
    {
        public SimianAssetPlugin()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {

        }

        public AssetServiceInterface Instantiate(string url)
        {
            return new Asset.SimianAssetConnector(url, (string)UUID.Zero);
        }

        public override string Name
        {
            get
            {
                return "opensim-simian";
            }
        }
    }

    [PluginName("AssetPlugin")]
    public class SimianAssetPluginFactory : IPluginFactory
    {
        public SimianAssetPluginFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new SimianAssetPlugin();
        }
    }
}
