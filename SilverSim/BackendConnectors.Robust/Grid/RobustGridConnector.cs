// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.Types;
using SilverSim.Types.Grid;
using System;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Grid
{
    #region Service Implementation
    public class RobustGridConnector : GridServiceInterface, IPlugin
    {
        string m_GridURI;
        public int TimeoutMs { get; set; }

        #region Constructor
        public RobustGridConnector(string uri)
        {
            TimeoutMs = 20000;
            if(!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "grid";
            m_GridURI = uri;
        }

        public void Startup(ConfigurationLoader loader)
        {

        }
        #endregion

        #region Accessors
        public override RegionInfo this[UUID scopeID, UUID regionID]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["SCOPEID"] = (string)scopeID;
                post["REGIONID"] = regionID.ToString();
                post["METHOD"] = "get_region_by_uuid";
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
                {
                    return DeserializeRegion(OpenSimResponse.Deserialize(s));
                }
            }
        }

        public override RegionInfo this[UUID scopeID, uint gridX, uint gridY]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["SCOPEID"] = (string)scopeID;
                post["X"] = gridX.ToString();
                post["Y"] = gridY.ToString();
                post["METHOD"] = "get_region_by_position";
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
                {
                    return DeserializeRegion(OpenSimResponse.Deserialize(s));
                }
            }
        }

        public override RegionInfo this[UUID scopeID, string regionName]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["SCOPEID"] = (string)scopeID;
                post["NAME"] = regionName;
                post["METHOD"] = "get_region_by_name";
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
                {
                    return DeserializeRegion(OpenSimResponse.Deserialize(s));
                }
            }
        }

        public override RegionInfo this[UUID regionID]
        {
            get
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["SCOPEID"] = (string)UUID.Zero;
                post["REGIONID"] = regionID.ToString();
                post["METHOD"] = "get_region_by_uuid";
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
                {
                    return DeserializeRegion(OpenSimResponse.Deserialize(s));
                }
            }
        }

        #endregion

        private void CheckResult(Map map)
        {
            if (!map.ContainsKey("Result"))
            {
                throw new GridRegionUpdateFailedException();
            }
            if (map["Result"].ToString().ToLower() != "success")
            {
                throw new GridRegionUpdateFailedException();
            }
        }

        #region Region Registration
        public override void RegisterRegion(RegionInfo regionInfo)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "register";
            post["uuid"] = (string)regionInfo.ID;
            post["locX"] = regionInfo.Location.X.ToString();
            post["locY"] = regionInfo.Location.Y.ToString();
            post["sizeX"] = regionInfo.Size.X.ToString();
            post["sizeY"] = regionInfo.Size.Y.ToString();
            post["regionName"] = regionInfo.Name;
            post["serverIP"] = regionInfo.ServerIP;
            post["serverHttpPort"] = regionInfo.ServerHttpPort.ToString();
            post["serverURI"] = regionInfo.ServerURI;
            post["serverPort"] = regionInfo.ServerPort.ToString();
            post["regionMapTexture"] = (string)regionInfo.RegionMapTexture;
            post["parcelMapTexture"] = (string)regionInfo.ParcelMapTexture;
            post["access"] = ((uint)regionInfo.Access).ToString();
            post["regionSecret"] = regionInfo.RegionSecret;
            post["owner_uuid"] = (string)regionInfo.Owner.ID;
            post["Token"] = string.Empty;
            post["SCOPEID"] = (string)UUID.Zero;
            post["VERSIONMIN"] = "0";
            post["VERSIONMAX"] = "1";

            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void UnregisterRegion(UUID scopeID, UUID regionID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["SCOPEID"] = (string)scopeID;
            post["REGIONID"] = (string)regionID;
            post["METHOD"] = "deregister";
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void DeleteRegion(UUID scopeID, UUID regionID)
        {
            throw new NotSupportedException();
        }
        #endregion

        #region List accessors
        private List<RegionInfo> DeserializeList(Map map)
        {
            List<RegionInfo> rl = new List<RegionInfo>();
            foreach(IValue i in map.Values)
            {
                Map m = i as Map;
                if(m != null)
                {
                    RegionInfo r = Deserialize(m);
                    if (r != null)
                    {
                        rl.Add(r);
                    }
                }
            }
            return rl;
        }

        private RegionInfo DeserializeRegion(Map map)
        {
            Map m = map["result"] as Map;
            if(null != m)
            {
                RegionInfo r = Deserialize(m);
                if(r == null)
                {
                    throw new GridRegionNotFoundException();
                }
                return r;
            }
            else
            {
                throw new GridServiceInaccessibleException();
            }
        }

        public override List<RegionInfo> GetDefaultRegions(UUID scopeID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "get_default_regions";
            Map res;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetFallbackRegions(UUID scopeID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "get_fallback_regions";
            Map res;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetDefaultHypergridRegions(UUID scopeID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["SCOPEID"] = (string)scopeID;
            post["METHOD"] = "get_default_hypergrid_regions";
            Map res;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetRegionsByRange(UUID scopeID, GridVector min, GridVector max)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["SCOPEID"] = (string)scopeID;
            post["XMIN"] = min.X.ToString();
            post["YMIN"] = min.Y.ToString();
            post["XMAX"] = max.X.ToString();
            post["YMAX"] = max.Y.ToString();
            post["METHOD"] = "get_region_range";
            Map res;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetNeighbours(UUID scopeID, UUID regionID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["SCOPEID"] = (string)scopeID;
            post["REGIONID"] = (string)regionID;
            post["METHOD"] = "get_neighbours";
            Map res;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetAllRegions(UUID scopeID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["SCOPEID"] = (string)scopeID;
            post["XMIN"] = "0";
            post["YMIN"] = "0";
            post["XMAX"] = "65535";
            post["YMAX"] = "65535";
            post["METHOD"] = "get_region_range";
            Map res;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetOnlineRegions(UUID scopeID)
        {
            List<RegionInfo> allRegions = GetAllRegions(scopeID);
            List<RegionInfo> onlineRegions = new List<RegionInfo>();
            foreach(RegionInfo ri in allRegions)
            {
                if((ri.Flags & RegionFlags.RegionOnline) != 0)
                {
                    onlineRegions.Add(ri);
                }
            }
            return onlineRegions;
        }

        public override List<RegionInfo> GetOnlineRegions()
        {
            throw new NotSupportedException();
        }

        public override List<RegionInfo> SearchRegionsByName(UUID scopeID, string searchString)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["SCOPEID"] = (string)scopeID;
            post["NAME"] = searchString;
            post["METHOD"] = "get_regions_by_name";
            Map res;
            using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override Dictionary<string, string> GetGridExtraFeatures()
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["METHOD"] = "get_grid_extra_features";
            Map m;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_GridURI, null, post, false, TimeoutMs))
            {
                m = OpenSimResponse.Deserialize(s) as Map;
            }
            if(null == m)
            {
                throw new NotSupportedException();
            }
            Dictionary<string, string> res = new Dictionary<string, string>();
            foreach(KeyValuePair<string, IValue> kvp in m)
            {
                res[kvp.Key] = kvp.Value.ToString();
            }
            return res;
        }

        private RegionInfo Deserialize(Map map)
        {
            RegionInfo r = new RegionInfo();
            r.ID = map["uuid"].ToString();
            r.Location.X = map["locX"].AsUInt;
            r.Location.Y = map["locY"].AsUInt;
            if(map.ContainsKey("flags"))
            {
                r.Flags = (RegionFlags)map["flags"].AsUInt;
            }
            else
            {
                r.Flags = RegionFlags.RegionOnline;
            }
            r.Size.X = map["sizeX"].AsUInt;
            r.Size.Y = map["sizeY"].AsUInt;
            r.Name = map["regionName"].ToString();
            r.ServerIP = map["serverIP"].ToString();
            r.ServerHttpPort = map["serverHttpPort"].AsUInt;
            r.ServerURI = map["serverURI"].ToString();
            r.ServerPort = map["serverPort"].AsUInt;
            r.RegionMapTexture = map["regionMapTexture"].AsUUID;
            r.ParcelMapTexture = map["parcelMapTexture"].AsUUID;
            r.Access = (RegionAccess)(byte)map["access"].AsUInt;
            r.RegionSecret = map["regionSecret"].ToString();
            r.Owner.ID = map["owner_uuid"].AsUUID;
            r.ProtocolVariant = RegionInfo.ProtocolVariantId.OpenSim;
            if(!Uri.IsWellFormedUriString(r.ServerURI, UriKind.Absolute) ||
                r.ServerPort == 0 ||
                r.ServerHttpPort == 0)
            {
                return null;
            }
            return r;
        }
        #endregion
    }
    #endregion

    #region Factory
    [PluginName("Grid")]
    public class RobustGridConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRID CONNECTOR");
        public RobustGridConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustGridConnector(ownSection.GetString("URI"));
        }
    }
    #endregion

}
