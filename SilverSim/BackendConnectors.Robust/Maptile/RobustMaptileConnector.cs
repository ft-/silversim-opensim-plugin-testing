// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Maptile;
using SilverSim.Types;
using SilverSim.Types.Maptile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web;

namespace SilverSim.BackendConnectors.Robust.Maptile
{
    public class RobustMaptileConnector : MaptileServiceInterface, IPlugin
    {
        readonly string m_Url;

        public int TimeoutMs { get; set; }

        public RobustMaptileConnector(string url)
        {
            TimeoutMs = 20000;
            m_Url = url;
            if(!m_Url.EndsWith("/"))
            {
                m_Url += "/";
            }
        }

        public override List<MaptileInfo> GetUpdateTimes(UUID scopeid, GridVector minloc, GridVector maxloc, int zoomlevel)
        {
            return new List<MaptileInfo>();
        }

        public override bool Remove(UUID scopeid, GridVector location, int zoomlevel)
        {
            return false;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* intentionally left empty */
        }

        public override void Store(MaptileData data)
        {
            if(data.ZoomLevel != 1)
            {
                return;
            }

            Dictionary<string, string> postVals = new Dictionary<string, string>();
            postVals.Add("X", data.Location.X.ToString());
            postVals.Add("Y", data.Location.Y.ToString());
            postVals.Add("DATA", Convert.ToBase64String(data.Data));
            postVals.Add("TYPE", data.ContentType);
            postVals.Add("SCOPEID", data.ScopeID.ToString());
            HttpClient.DoPostRequest(m_Url + "map", null, postVals, false, TimeoutMs);
        }

        public override bool TryGetValue(UUID scopeid, GridVector location, int zoomlevel, out MaptileData data)
        {
            string requrl = m_Url + string.Format("map-{0}-{1}-{2}-0.jpg", zoomlevel, location.X, location.Y);
            try
            {
                using (Stream s = HttpClient.DoStreamGetRequest(requrl, null, TimeoutMs))
                {
                    data = new MaptileData();
                    data.Location = location;
                    data.ZoomLevel = zoomlevel;
                    data.ScopeID = scopeid;
                    data.LastUpdate = Date.Now;
                    data.Data = s.ReadToStreamEnd();
                    data.ContentType = "image/jpeg";
                    return true;
                }
            }
            catch(HttpException e)
            {
                if(e.GetHttpCode() == (int)HttpStatusCode.NotFound)
                {
                    data = null;
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }
    }

    [PluginName("Maptile")]
    public class RobustMaptileConnectorFactory : IPluginFactory
    {
        public RobustMaptileConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustMaptileConnector(ownSection.GetString("URI"));
        }
    }
}