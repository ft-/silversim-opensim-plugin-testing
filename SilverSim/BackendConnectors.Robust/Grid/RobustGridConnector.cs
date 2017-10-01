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
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.Types;
using SilverSim.Types.Grid;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.Grid
{
    [Description("Robust Grid Connector")]
    [PluginName("Grid")]
    public partial class RobustGridConnector : GridServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRID CONNECTOR");
        private readonly string m_GridURI;
        public int TimeoutMs { get; set; }

        #region Constructor
        public RobustGridConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            string uri = ownSection.GetString("URI");

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
            /* no action needed */
        }
        #endregion

        #region Accessors
        public override bool TryGetValue(UUID scopeID, UUID regionID, out RegionInfo rInfo)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["REGIONID"] = regionID.ToString(),
                ["METHOD"] = "get_region_by_uuid"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                return TryDeserializeRegion(OpenSimResponse.Deserialize(s), out rInfo);
            }
        }

        public override bool ContainsKey(UUID scopeID, UUID regionID)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["REGIONID"] = regionID.ToString(),
                ["METHOD"] = "get_region_by_uuid"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                return TryDeserializeRegion(OpenSimResponse.Deserialize(s));
            }
        }

        public override RegionInfo this[UUID scopeID, UUID regionID]
        {
            get
            {
                var post = new Dictionary<string, string>
                {
                    ["SCOPEID"] = (string)scopeID,
                    ["REGIONID"] = regionID.ToString(),
                    ["METHOD"] = "get_region_by_uuid"
                };
                using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                {
                    return DeserializeRegion(OpenSimResponse.Deserialize(s));
                }
            }
        }

        public override bool TryGetValue(UUID scopeID, uint gridX, uint gridY, out RegionInfo rInfo)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["X"] = gridX.ToString(),
                ["Y"] = gridY.ToString(),
                ["METHOD"] = "get_region_by_position"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                return TryDeserializeRegion(OpenSimResponse.Deserialize(s), out rInfo);
            }
        }

        public override bool ContainsKey(UUID scopeID, uint gridX, uint gridY)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["X"] = gridX.ToString(),
                ["Y"] = gridY.ToString(),
                ["METHOD"] = "get_region_by_position"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                return TryDeserializeRegion(OpenSimResponse.Deserialize(s));
            }
        }

        public override RegionInfo this[UUID scopeID, uint gridX, uint gridY]
        {
            get
            {
                var post = new Dictionary<string, string>
                {
                    ["SCOPEID"] = (string)scopeID,
                    ["X"] = gridX.ToString(),
                    ["Y"] = gridY.ToString(),
                    ["METHOD"] = "get_region_by_position"
                };
                using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                {
                    return DeserializeRegion(OpenSimResponse.Deserialize(s));
                }
            }
        }

        public override bool TryGetValue(UUID scopeID, string regionName, out RegionInfo rInfo)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["NAME"] = regionName,
                ["METHOD"] = "get_region_by_name"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                return TryDeserializeRegion(OpenSimResponse.Deserialize(s), out rInfo);
            }
        }

        public override bool ContainsKey(UUID scopeID, string regionName)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["NAME"] = regionName,
                ["METHOD"] = "get_region_by_name"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                return TryDeserializeRegion(OpenSimResponse.Deserialize(s));
            }
        }

        public override RegionInfo this[UUID scopeID, string regionName]
        {
            get
            {
                var post = new Dictionary<string, string>
                {
                    ["SCOPEID"] = (string)scopeID,
                    ["NAME"] = regionName,
                    ["METHOD"] = "get_region_by_name"
                };
                using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                {
                    return DeserializeRegion(OpenSimResponse.Deserialize(s));
                }
            }
        }

        public override bool TryGetValue(UUID regionID, out RegionInfo rInfo)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)UUID.Zero,
                ["REGIONID"] = regionID.ToString(),
                ["METHOD"] = "get_region_by_uuid"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                return TryDeserializeRegion(OpenSimResponse.Deserialize(s), out rInfo);
            }
        }

        public override bool ContainsKey(UUID regionID)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)UUID.Zero,
                ["REGIONID"] = regionID.ToString(),
                ["METHOD"] = "get_region_by_uuid"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                return TryDeserializeRegion(OpenSimResponse.Deserialize(s));
            }
        }

        public override RegionInfo this[UUID regionID]
        {
            get
            {
                var post = new Dictionary<string, string>
                {
                    ["SCOPEID"] = (string)UUID.Zero,
                    ["REGIONID"] = regionID.ToString(),
                    ["METHOD"] = "get_region_by_uuid"
                };
                using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
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
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "register",
                ["uuid"] = (string)regionInfo.ID,
                ["locX"] = regionInfo.Location.X.ToString(),
                ["locY"] = regionInfo.Location.Y.ToString(),
                ["sizeX"] = regionInfo.Size.X.ToString(),
                ["sizeY"] = regionInfo.Size.Y.ToString(),
                ["regionName"] = regionInfo.Name,
                ["serverIP"] = regionInfo.ServerIP,
                ["serverHttpPort"] = regionInfo.ServerHttpPort.ToString(),
                ["serverURI"] = regionInfo.ServerURI,
                ["serverPort"] = regionInfo.ServerPort.ToString(),
                ["regionMapTexture"] = (string)regionInfo.RegionMapTexture,
                ["parcelMapTexture"] = (string)regionInfo.ParcelMapTexture,
                ["access"] = ((uint)regionInfo.Access).ToString(),
                ["regionSecret"] = regionInfo.RegionSecret,
                ["owner_uuid"] = (string)regionInfo.Owner.ID,
                ["Token"] = string.Empty,
                ["SCOPEID"] = (string)UUID.Zero,
                ["VERSIONMIN"] = "0",
                ["VERSIONMAX"] = "1"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                CheckResult(OpenSimResponse.Deserialize(s));
            }
        }

        public override void UnregisterRegion(UUID scopeID, UUID regionID)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["REGIONID"] = (string)regionID,
                ["METHOD"] = "deregister"
            };
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
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
            var rl = new List<RegionInfo>();
            foreach(IValue i in map.Values)
            {
                var m = i as Map;
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
            RegionInfo rInfo;
            if(!map.ContainsKey("result"))
            {
                throw new GridServiceInaccessibleException();
            }

            if(!TryDeserializeRegion(map, out rInfo))
            {
                throw new GridRegionNotFoundException();
            }

            return rInfo;
        }

        private bool TryDeserializeRegion(Map map, out RegionInfo r)
        {
            var m = map["result"] as Map;
            if (m != null)
            {
                r = Deserialize(m);
                if (r == null)
                {
                    return false;
                }
                return true;
            }
            else
            {
                r = default(RegionInfo);
                return false;
            }
        }

        private bool TryDeserializeRegion(Map map)
        {
            var m = map["result"] as Map;
            if (m != null)
            {
                RegionInfo r = Deserialize(m);
                if (r == null)
                {
                    return false;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public override List<RegionInfo> GetHyperlinks(UUID scopeID)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "get_hyperlinks"
            };
            Map res;
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetDefaultRegions(UUID scopeID)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "get_default_regions"
            };
            Map res;
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetFallbackRegions(UUID scopeID)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "get_fallback_regions"
            };
            Map res;
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetDefaultHypergridRegions(UUID scopeID)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["METHOD"] = "get_default_hypergrid_regions"
            };
            Map res;
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetRegionsByRange(UUID scopeID, GridVector min, GridVector max)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["XMIN"] = min.X.ToString(),
                ["YMIN"] = min.Y.ToString(),
                ["XMAX"] = max.X.ToString(),
                ["YMAX"] = max.Y.ToString(),
                ["METHOD"] = "get_region_range"
            };
            Map res;
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetNeighbours(UUID scopeID, UUID regionID)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["REGIONID"] = (string)regionID,
                ["METHOD"] = "get_neighbours"
            };
            Map res;
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetAllRegions(UUID scopeID)
        {
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["XMIN"] = "0",
                ["YMIN"] = "0",
                ["XMAX"] = "65535",
                ["YMAX"] = "65535",
                ["METHOD"] = "get_region_range"
            };
            Map res;
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override List<RegionInfo> GetOnlineRegions(UUID scopeID)
        {
            var onlineRegions = new List<RegionInfo>();
            foreach(RegionInfo ri in GetAllRegions(scopeID))
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
            var post = new Dictionary<string, string>
            {
                ["SCOPEID"] = (string)scopeID,
                ["NAME"] = searchString,
                ["METHOD"] = "get_regions_by_name"
            };
            Map res;
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }
            return DeserializeList(res);
        }

        public override Dictionary<string, string> GetGridExtraFeatures()
        {
            var post = new Dictionary<string, string>
            {
                ["METHOD"] = "get_grid_extra_features"
            };
            Map m;
            using (Stream s = new HttpClient.Post(m_GridURI, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
            {
                m = OpenSimResponse.Deserialize(s) as Map;
            }
            if(m == null)
            {
                throw new NotSupportedException();
            }
            var res = new Dictionary<string, string>();
            foreach(KeyValuePair<string, IValue> kvp in m)
            {
                res[kvp.Key] = kvp.Value.ToString();
            }
            return res;
        }

        private RegionInfo Deserialize(Map map)
        {
            var r = new RegionInfo()
            {
                ID = map["uuid"].ToString(),
                Location = new GridVector { X = map["locX"].AsUInt, Y = map["locY"].AsUInt },
                Flags = map.ContainsKey("flags") ? ((RegionFlags)map["flags"].AsUInt) : RegionFlags.RegionOnline,
                Size = new GridVector { X = map["sizeX"].AsUInt, Y = map["sizeY"].AsUInt },
                Name = map["regionName"].ToString(),
                ServerIP = map["serverIP"].ToString(),
                ServerHttpPort = map["serverHttpPort"].AsUInt,
                ServerURI = map["serverURI"].ToString(),
                ServerPort = map["serverPort"].AsUInt,
                RegionMapTexture = map["regionMapTexture"].AsUUID,
                ParcelMapTexture = map["parcelMapTexture"].AsUUID,
                Access = (RegionAccess)(byte)map["access"].AsUInt,
                RegionSecret = map["regionSecret"].ToString(),
                Owner = new UUI(map["owner_uuid"].AsUUID),
                ProtocolVariant = RegionInfo.ProtocolVariantId.OpenSim
            };
            if (!Uri.IsWellFormedUriString(r.ServerURI, UriKind.Absolute) ||
                r.ServerPort == 0 ||
                r.ServerHttpPort == 0)
            {
                return null;
            }
            return r;
        }
        #endregion
    }
}
