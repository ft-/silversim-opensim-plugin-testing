// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.Types;
using SilverSim.Types.IM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SilverSim.BackendConnectors.Robust.IM
{
    #region Service Implementation
    public class RobustOfflineIMConnector : OfflineIMServiceInterface, IPlugin
    {
        public int TimeoutMs { get; set; }
        string m_OfflineIMURI;
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

        }

        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        public override void storeOfflineIM(GridInstantMessage im)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["BinaryBucket"] = BitConverter.ToString(im.BinaryBucket).Replace("-", string.Empty);
            post["Dialog"] = ((int)im.Dialog).ToString();
            if (im.IsFromGroup)
            {
                post["FromAgentID"] = (string)im.FromGroup.ID;
            }
            else
            {
                post["FromAgentID"] = (string)im.FromAgent.ID;
            }
            post["FromAgentName"] = im.FromAgent.FullName;
            bool isFromGroup = !im.IsFromGroup.Equals(UUID.Zero);
            post["FromGroup"] = isFromGroup.ToString();
            post["Message"] = im.Message;
            post["EstateID"] = im.ParentEstateID.ToString();
            post["Position"] = im.Position.ToString();
            post["RegionID"] = (string)im.RegionID;
            post["Timestamp"] = im.Timestamp.DateTimeToUnixTime().ToString();
            post["ToAgentID"] = (string)im.ToAgent.ID;
            post["METHOD"] = "STORE";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_OfflineIMURI, null, post, false, TimeoutMs));
            if (!map.ContainsKey("RESULT"))
            {
                throw new IMOfflineStoreFailedException();
            }
            if (map["RESULT"].ToString().ToLower() == "false")
            {
                throw new IMOfflineStoreFailedException(map.ContainsKey("REASON") ? map["REASON"].ToString() : "Unknown Error");
            }
        }

        public override List<GridInstantMessage> getOfflineIMs(UUID principalID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["PrincipalID"] = (string)principalID;
            post["METHOD"] = "GET";
            Map map = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_OfflineIMURI, null, post, false, TimeoutMs));
            if (!map.ContainsKey("RESULT"))
            {
                throw new IMOfflineStoreFailedException();
            }
            if (!(map["RESULT"] is Map) || map["RESULT"].ToString().ToLower() == "false")
            {
                throw new IMOfflineStoreFailedException(map.ContainsKey("REASON") ? map["REASON"].ToString() : "Unknown Error");
            }
            List<GridInstantMessage> ims = new List<GridInstantMessage>();
            foreach(IValue v in ((Map)(map["RESULT"])).Values)
            {
                if(!(v is Map))
                {
                    continue;
                }
                Map m = (Map)v;

                GridInstantMessage im = new GridInstantMessage();
                im.BinaryBucket = StringToByteArray(m["BinaryBucket"].ToString());
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

        public override void deleteOfflineIM(ulong offlineImID)
        {

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
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new RobustOfflineIMConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
