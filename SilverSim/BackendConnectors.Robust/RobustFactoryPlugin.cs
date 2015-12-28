// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.ServiceInterfaces.UserAgents;
using System.ComponentModel;

namespace SilverSim.BackendConnectors.Robust
{
    #region Inventory Plugin
    [Description("Robust Inventory Connector Factory")]
    public class RobustInventoryPlugin : ServicePluginHelo, IInventoryServicePlugin, IPlugin
    {
        public RobustInventoryPlugin()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public InventoryServiceInterface Instantiate(string url)
        {
            return new Inventory.RobustInventoryConnector(url);
        }

        public override string Name
        {
            get
            {
                return "opensim-robust";
            }
        }
    }
    [PluginName("InventoryPlugin")]
    public class RobustInventoryPluginFactory : IPluginFactory
    {
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustInventoryPlugin();
        }
    }
    #endregion

    #region Asset plugin
    [Description("Robust Asset Connector Factory")]
    public class RobustAssetPlugin : ServicePluginHelo, IAssetServicePlugin, IPlugin
    {
        public RobustAssetPlugin()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public AssetServiceInterface Instantiate(string url)
        {
            return new Asset.RobustAssetConnector(url);
        }

        public override string Name
        {
            get
            {
                return "opensim-robust";
            }
        }
    }

    [PluginName("AssetPlugin")]
    public class RobustAssetPluginFactory : IPluginFactory
    {
        public RobustAssetPluginFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustAssetPlugin();
        }
    }
    #endregion

    #region UserAgent plugin
    [Description("Robust UserAgent Connector Factory")]
    public class RobustUserAgentPlugin : ServicePluginHelo, IUserAgentServicePlugin, IPlugin
    {
        readonly string m_Name;
        public RobustUserAgentPlugin(string name)
        {
            m_Name = name;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public UserAgentServiceInterface Instantiate(string url)
        {
            return new UserAgent.RobustUserAgentConnector(url);
        }

        public override string Name
        {
            get
            {
                return m_Name;
            }
        }
    }

    public class RobustUserAgentPluginSubFactory : IPlugin, IPluginSubFactory
    {
        public RobustUserAgentPluginSubFactory()
        {

        }



        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public void AddPlugins(ConfigurationLoader loader)
        {
            loader.AddPlugin("RobustUserAgentConnector", new RobustUserAgentPlugin("opensim-robust"));
            loader.AddPlugin("SimianUserAgentConnector", new RobustUserAgentPlugin("opensim-simian"));
        }
    }
    [PluginName("UserAgentPlugin")]
    public class RobustUserAgentPluginFactory : IPluginFactory
    {
        public RobustUserAgentPluginFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustUserAgentPluginSubFactory();
        }
    }
    #endregion
}
