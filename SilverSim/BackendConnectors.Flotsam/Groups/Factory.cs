// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    [PluginName("XmlRpcGroups")]
    public class Factory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("FLOTSAM GROUPS CONNECTOR");

        public Factory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if(!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new FlotsamGroupsConnector(ownSection.GetString("URI"));
        }
    }
}
