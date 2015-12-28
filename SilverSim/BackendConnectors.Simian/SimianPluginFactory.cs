// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using System.ComponentModel;

namespace SilverSim.BackendConnectors.Simian
{
    [Description("Simian Inventory Connector Factory")]
    public sealed class SimianInventoryPlugin : ServicePluginHelo, IInventoryServicePlugin, IPlugin
    {
        public SimianInventoryPlugin()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
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
    public sealed class SimianInventoryPluginFactory : IPluginFactory
    {
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new SimianInventoryPlugin();
        }
    }

    [Description("Simian Asset Connector Factory")]
    public sealed class SimianAssetPlugin : ServicePluginHelo, IAssetServicePlugin, IPlugin
    {
        public SimianAssetPlugin()
        {
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
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
    public sealed class SimianAssetPluginFactory : IPluginFactory
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
