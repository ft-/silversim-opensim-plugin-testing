// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3 with
// the following clarification and special exception.

// Linking this library statically or dynamically with other modules is
// making a combined work based on this library. Thus, the terms and
// conditions of the GNU Affero General Public License cover the whole
// combination.

// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module. An independent module is a module which is not derived from
// or based on this library. If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so. If you do not wish to do so, delete this
// exception statement from your version.

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.ServiceInterfaces.UserAgents;
using System.ComponentModel;
using System;

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

    #region Friends plugin
    [Description("Robust HGFriends Connector factory")]
    public class RobustHGFriendsPlugin : ServicePluginHelo, IFriendsServicePlugin, IPlugin
    {
        public RobustHGFriendsPlugin()
        {

        }

        public override string Name
        {
            get
            {
                return "opensim-robust";
            }
        }

        public FriendsServiceInterface Instantiate(string url)
        {
            return new Friends.RobustFriendsConnector(url, string.Empty);
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* intentionally left empty */
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

    [Description("Robust UserAgent Connector Factory Factory")]
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
