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
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.Types;
using SilverSim.Types.IM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace SilverSim.BackendConnectors.Robust.IM
{
    #region Service Implementation
    [Description("Robust OfflineIM Connector")]
    [PluginName("OfflineIM")]
    public class RobustOfflineIMConnector : OfflineIMServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST OFFLINE IM CONNECTOR");

        public int TimeoutMs { get; set; }
        private readonly string m_OfflineIMURI;
        public RobustOfflineIMConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            string uri = ownSection.GetString("URI");
            TimeoutMs = 20000;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "offlineim";
            m_OfflineIMURI = uri;
        }

        public RobustOfflineIMConnector(string uri)
        {
            TimeoutMs = 20000;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "offlineim";
            m_OfflineIMURI = uri;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action required */
        }

        public override void StoreOfflineIM(GridInstantMessage im)
        {
            bool isFromGroup = im.IsFromGroup;
            var post = new Dictionary<string, string>
            {
                ["BinaryBucket"] = im.BinaryBucket.ToHexString(),
                ["Dialog"] = ((int)im.Dialog).ToString(),
                ["FromAgentID"] = isFromGroup ? (string)im.FromGroup.ID : (string)im.FromAgent.ID,
                ["FromAgentName"] = im.FromAgent.FullName,
                ["FromGroup"] = isFromGroup.ToString(),
                ["Message"] = im.Message,
                ["EstateID"] = im.ParentEstateID.ToString(),
                ["Position"] = im.Position.ToString(),
                ["RegionID"] = (string)im.RegionID,
                ["Timestamp"] = im.Timestamp.DateTimeToUnixTime().ToString(),
                ["ToAgentID"] = (string)im.ToAgent.ID,
                ["METHOD"] = "STORE"
            };
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_OfflineIMURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("RESULT"))
            {
                throw new IMOfflineStoreFailedException();
            }
            if (map["RESULT"].ToString().ToLower() == "false")
            {
                throw new IMOfflineStoreFailedException(map.ContainsKey("REASON") ? map["REASON"].ToString() : "Unknown Error");
            }
        }

        public override List<GridInstantMessage> GetOfflineIMs(UUID principalID)
        {
            var post = new Dictionary<string, string>
            {
                ["PrincipalID"] = (string)principalID,
                ["METHOD"] = "GET"
            };
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_OfflineIMURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("RESULT"))
            {
                throw new IMOfflineStoreFailedException();
            }
            var resultmap = map["RESULT"] as Map;
            if (resultmap == null || map["RESULT"].ToString().ToLower() == "false")
            {
                throw new IMOfflineStoreFailedException(map.ContainsKey("REASON") ? map["REASON"].ToString() : "Unknown Error");
            }
            var ims = new List<GridInstantMessage>();
            foreach(IValue v in resultmap.Values)
            {
                var m = v as Map;
                if (m == null)
                {
                    continue;
                }

                var im = new GridInstantMessage()
                {
                    BinaryBucket = m["BinaryBucket"].ToString().FromHexStringToByteArray(),
                    Dialog = (GridInstantMessageDialog)m["Dialog"].AsInt,
                    FromAgent = new UUI { ID = m["FromAgentID"].ToString(), FullName = m["FromAgentName"].ToString() },
                    FromGroup = new UGI(m["FromAgentID"].ToString()),
                    IsFromGroup = m["FromGroup"].AsBoolean,
                    IMSessionID = m["SessionID"].ToString(),
                    Message = m["Message"].ToString(),
                    IsOffline = m["Offline"].AsBoolean,
                    ParentEstateID = m["EstateID"].AsUInt,
                    Position = m["Position"].AsVector3,
                    RegionID = m["RegionID"].AsString.ToString(),
                    Timestamp = Date.UnixTimeToDateTime(m["Timestamp"].AsULong),
                    ToAgent = new UUI(m["ToAgentID"].ToString())
                };
                ims.Add(im);
            }
            return ims;
        }

        public override void DeleteOfflineIM(ulong offlineImID)
        {
            /* no action required */
        }
    }
    #endregion
}
