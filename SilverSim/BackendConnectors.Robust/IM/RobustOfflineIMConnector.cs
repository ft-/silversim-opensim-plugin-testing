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
    public class RobustOfflineIMConnector : OfflineIMServiceInterface, IPlugin
    {
        public int TimeoutMs { get; set; }
        readonly string m_OfflineIMURI;
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            bool isFromGroup = im.IsFromGroup;
            post["BinaryBucket"] = im.BinaryBucket.ToHexString();
            post["Dialog"] = ((int)im.Dialog).ToString();
            post["FromAgentID"] = isFromGroup ? (string)im.FromGroup.ID : (string)im.FromAgent.ID;
            post["FromAgentName"] = im.FromAgent.FullName;
            post["FromGroup"] = isFromGroup.ToString();
            post["Message"] = im.Message;
            post["EstateID"] = im.ParentEstateID.ToString();
            post["Position"] = im.Position.ToString();
            post["RegionID"] = (string)im.RegionID;
            post["Timestamp"] = im.Timestamp.DateTimeToUnixTime().ToString();
            post["ToAgentID"] = (string)im.ToAgent.ID;
            post["METHOD"] = "STORE";
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PrincipalID"] = (string)principalID;
            post["METHOD"] = "GET";
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_OfflineIMURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("RESULT"))
            {
                throw new IMOfflineStoreFailedException();
            }
            Map resultmap = map["RESULT"] as Map;
            if (null == resultmap || map["RESULT"].ToString().ToLower() == "false")
            {
                throw new IMOfflineStoreFailedException(map.ContainsKey("REASON") ? map["REASON"].ToString() : "Unknown Error");
            }
            List<GridInstantMessage> ims = new List<GridInstantMessage>();
            foreach(IValue v in resultmap.Values)
            {
                Map m = v as Map;
                if (null == m)
                {
                    continue;
                }

                GridInstantMessage im = new GridInstantMessage();
                im.BinaryBucket = m["BinaryBucket"].ToString().FromHexStringToByteArray();
                im.Dialog = (GridInstantMessageDialog) m["Dialog"].AsInt;
                im.FromAgent.ID = m["FromAgentID"].ToString();
                im.FromAgent.FullName = m["FromAgentName"].ToString();
                im.FromGroup.ID = m["FromAgentID"].ToString();
                im.IsFromGroup = m["FromGroup"].AsBoolean;
                im.IMSessionID = m["SessionID"].ToString();
                im.Message = m["Message"].ToString();
                im.IsOffline = m["Offline"].AsBoolean;
                im.ParentEstateID = m["EstateID"].AsUInt;
                im.Position = m["Position"].AsVector3;
                im.RegionID = m["RegionID"].AsString.ToString();
                im.Timestamp = Date.UnixTimeToDateTime(m["Timestamp"].AsULong);
                im.ToAgent.ID = m["ToAgentID"].ToString();
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

    #region Factory
    [PluginName("OfflineIM")]
    public class RobustOfflineIMConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST OFFLINE IM CONNECTOR");
        public RobustOfflineIMConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustOfflineIMConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
