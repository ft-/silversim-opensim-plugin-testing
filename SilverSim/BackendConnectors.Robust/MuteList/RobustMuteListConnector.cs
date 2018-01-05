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
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.MuteList;
using SilverSim.Types;
using SilverSim.Types.MuteList;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using SilverSim.Http.Client;
using SilverSim.BackendConnectors.Robust.Common;

namespace SilverSim.BackendConnectors.Robust.MuteList
{
    [Description("Robust MuteList Connector")]
    [PluginName("MuteList")]
    public sealed class RobustMuteListConnector : MuteListServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST MUTELIST CONNECTOR");

        private readonly string m_Uri;
        private string m_HomeUri;
        public int TimeoutMs = 20000;

        public RobustMuteListConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }

            m_Uri = ownSection.GetString("URI");
            if (!m_Uri.EndsWith("/"))
            {
                m_Uri += "/";
            }
            m_Uri += "mutelist";
            m_HomeUri = string.Empty;
        }

        public RobustMuteListConnector(string uri, string homeuri)
        {
            m_Uri = uri;
            if (!m_Uri.EndsWith("/"))
            {
                m_Uri += "/";
            }
            m_Uri += "mutelist";

            if (homeuri.Length != 0)
            {
                m_HomeUri = homeuri;
                if (!m_HomeUri.EndsWith("/"))
                {
                    m_HomeUri += "/";
                }
            }
            else
            {
                m_HomeUri = string.Empty;
            }
        }

        private void CheckResult(Map map)
        {
            if (!map.ContainsKey("result"))
            {
                throw new InvalidOperationException();
            }
            if (string.Equals(map["result"].ToString(), "failure", StringComparison.CurrentCultureIgnoreCase))
            {
                throw new InvalidOperationException();
            }
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* only called when initialized by ConfigurationLoader */
            m_HomeUri = loader.HomeURI;
        }

        public override List<MuteListEntry> GetList(UUID muteListOwnerID, uint crc)
        {
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "get",
                ["agentid"] = muteListOwnerID.ToString(),
                ["mutecrc"] = crc.ToString()
            };
            byte[] mutelist_data;
            using (Stream s = new HttpClient.Post(m_Uri, post)
            {
                TimeoutMs = TimeoutMs
            }.ExecuteStreamRequest())
            {
                Map res = OpenSimResponse.Deserialize(s);
                CheckResult(res);
                mutelist_data = Convert.FromBase64String(res["result"].ToString());
            }
            /* interpret use cached signal */
            if(mutelist_data.Length == 1 && mutelist_data[0] == 1)
            {
                throw new UseCachedMuteListException();
            }
            return mutelist_data.ToMuteList();
        }

        public override void Store(UUID muteListOwnerID, MuteListEntry mute)
        {
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "update",
                ["agentid"] = muteListOwnerID.ToString(),
                ["muteid"] = mute.MuteID.ToString(),
                ["mutename"] = mute.MuteName,
                ["mutetype"] = ((int)mute.Type).ToString(),
                ["muteflags"] = ((uint)mute.Flags).ToString()
            };
            using (Stream s = new HttpClient.Post(m_Uri, post)
            {
                TimeoutMs = TimeoutMs
            }.ExecuteStreamRequest())
            {
                Map res = OpenSimResponse.Deserialize(s);
                CheckResult(res);
            }
        }

        public override bool Remove(UUID muteListOwnerID, UUID muteID, string muteName)
        {
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "delete",
                ["agentid"] = muteListOwnerID.ToString(),
                ["muteid"] = muteID.ToString(),
                ["mutename"] = muteName,
            };
            using (Stream s = new HttpClient.Post(m_Uri, post)
            {
                TimeoutMs = TimeoutMs
            }.ExecuteStreamRequest())
            {
                Map res = OpenSimResponse.Deserialize(s);
                string res_str;
                return res.TryGetValue("result", out res_str) && res_str.Equals("success", StringComparison.CurrentCultureIgnoreCase);
            }
        }
    }
}