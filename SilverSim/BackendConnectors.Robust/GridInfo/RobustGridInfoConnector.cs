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

using log4net;
using Nini.Config;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.Types;
using SilverSim.Types.StructuredData.Json;
using System.ComponentModel;
using System.IO;
using System.Web;

namespace SilverSim.BackendConnectors.Robust.GridInfo
{
    #region Service Implementation
    [Description("Robust GridInfo Connector")]
    public class RobustGridInfoConnector : GridInfoServiceInterface, IPlugin
    {
        private readonly string m_GridURI;
        public int TimeoutMs { get; set; }
        private readonly object m_UpdateLock = new object();
        private Map m_CachedGridInfo = new Map();

        private Date m_LastUpdate = new Date();

        #region Constructor
        public RobustGridInfoConnector(string uri)
        {
            TimeoutMs = 20000;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "grid";
            m_GridURI = uri;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        public override string GridNick
        {
            get
            {
                CheckCache();
                lock (m_CachedGridInfo)
                {
                    IValue value;
                    return (m_CachedGridInfo.TryGetValue("gridnick", out value)) ? value.ToString() : "error";
                }
            }
        }

        public override string GridName
        {
            get
            {
                CheckCache();
                lock (m_CachedGridInfo)
                {
                    IValue value;
                    return (m_CachedGridInfo.TryGetValue("gridname", out value)) ? value.ToString() : "error";
                }
            }
        }

        public override string LoginURI
        {
            get
            {
                CheckCache();
                lock (m_CachedGridInfo)
                {
                    IValue value;
                    return (m_CachedGridInfo.TryGetValue("login", out value)) ? value.ToString() : "error";
                }
            }
        }

        public override string HomeURI
        {
            get
            {
                CheckCache();
                lock (m_CachedGridInfo)
                {
                    IValue value;
                    return (m_CachedGridInfo.TryGetValue("home", out value)) ? value.ToString() : "error";
                }
            }
        }

        public override string this[string key]
        {
            get
            {
                CheckCache();
                return m_CachedGridInfo[key].ToString();
            }
        }

        public override bool ContainsKey(string key)
        {
            CheckCache();
            return m_CachedGridInfo.ContainsKey(key);
        }

        public override bool TryGetValue(string key, out string value)
        {
            IValue ivalue;
            CheckCache();
            if (m_CachedGridInfo.TryGetValue(key, out ivalue))
            {
                value = ivalue.ToString();
                return true;
            }
            value = string.Empty;
            return false;
        }

        private void CheckCache()
        {
            ulong age;

            lock(m_UpdateLock)
            {
                age = Date.GetUnixTime() - m_LastUpdate.AsULong;
            }

            if(age > 24 * 3600)
            {
                Map m;
                try
                {
                    using (Stream s = HttpClient.DoStreamGetRequest(m_GridURI + "/json_grid_info", null, TimeoutMs))
                    {
                        m = Json.Deserialize(s) as Map;
                    }
                }
                catch(HttpException)
                {
                    return;
                }

                if(m == null)
                {
                    return;
                }

                lock(m_UpdateLock)
                {
                    m_CachedGridInfo = m;
                    m_LastUpdate = new Date();
                }
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("GridInfo")]
    public class RobustGridInfoConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRIDINFO CONNECTOR");

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustGridInfoConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
