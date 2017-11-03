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
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types;
using SilverSim.ServiceInterfaces.ServerParam;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.ComponentModel;
using SilverSim.Threading;

namespace SilverSim.BackendHandlers.Robust.Simulation
{
    [Description("OpenSim PostAgent Direct HG Handler")]
    [ServerParam("DirectHGEnabled", ParameterType = typeof(bool), DefaultValue = false)]
    [ServerParam("DefaultHGRegion", ParameterType = typeof(string), DefaultValue = "", Type = ServerParamType.GlobalOnly)]
    [PluginName("RobustDirectHGHandler")]
    public class PostAgentHGDirectHandler : PostAgentHandler
    {
        private HttpXmlRpcHandler m_XmlRpcServer;

        public PostAgentHGDirectHandler(IConfig ownSection)
            : base("/foreignagent/", ownSection)
        {
            if(ownSection.GetBoolean("DirectHGEnabledByDefault", false))
            {
                m_HGDirectEnabled[UUID.Zero] = true;
            }
        }

        public override void Startup(ConfigurationLoader loader)
        {
            base.Startup(loader);
            m_Log.Info("Initializing DirectHG XMLRPC handlers");
            m_XmlRpcServer = loader.GetService<HttpXmlRpcHandler>("XmlRpcServer");
            m_XmlRpcServer.XmlRpcMethods.Add("link_region", LinkRegion);
            m_XmlRpcServer.XmlRpcMethods.Add("get_region", GetRegion);
        }

        public override void Shutdown()
        {
            m_XmlRpcServer.XmlRpcMethods.Remove("link_region");
            m_XmlRpcServer.XmlRpcMethods.Remove("get_region");
            base.Shutdown();
        }

        protected new void CheckScenePerms(UUID sceneID)
        {
            if (!GetHGDirectEnabled(sceneID))
            {
                throw new InvalidOperationException("No HG Direct access to scene");
            }
        }

        private XmlRpc.XmlRpcResponse LinkRegion(XmlRpc.XmlRpcRequest req)
        {
            string region_name = string.Empty;
            try
            {
                region_name = (((Map)req.Params[0])["region_name"]).ToString();
            }
            catch
            {
                region_name = string.Empty;
            }
            Map resdata = new Map();
            if (string.IsNullOrEmpty(region_name))
            {
                region_name = m_DefaultHGRegion;
            }

            if(!string.IsNullOrEmpty(region_name))
            {
                try
                {
                    SceneInterface s = Scenes[region_name];
                    if (GetHGDirectEnabled(s.ID))
                    {
                        resdata.Add("uuid", s.ID);
                        resdata.Add("handle", s.GridPosition.RegionHandle.ToString());
                        resdata.Add("region_image", string.Empty);
                        resdata.Add("external_name", s.ServerURI + " " + s.Name);
                        resdata.Add("result", true);
                    }
                }
                catch
                {
                    resdata.Add("result", false);
                }
            }
            else
            {
                resdata.Add("result", false);
            }
            return new XmlRpc.XmlRpcResponse
            {
                ReturnValue = resdata
            };
        }

        private XmlRpc.XmlRpcResponse GetRegion(XmlRpc.XmlRpcRequest req)
        {
            UUID region_uuid = (((Map)req.Params[0])["region_uuid"]).AsUUID;
            var resdata = new Map();
            try
            {
                SceneInterface s = Scenes[region_uuid];
                if (GetHGDirectEnabled(s.ID))
                {
                    resdata.Add("uuid", s.ID);
                    resdata.Add("handle", s.GridPosition.RegionHandle.ToString());
                    resdata.Add("x", s.GridPosition.X.ToString());
                    resdata.Add("y", s.GridPosition.Y.ToString());
                    resdata.Add("size_x", s.SizeX.ToString());
                    resdata.Add("size_y", s.SizeY.ToString());
                    resdata.Add("region_name", s.Name);
                    Uri serverURI = new Uri(s.ServerURI);
                    resdata.Add("hostname", serverURI.Host);
                    resdata.Add("http_port", s.ServerHttpPort);
                    resdata.Add("internal_port", s.RegionPort);
                    resdata.Add("server_uri", s.ServerURI);
                    resdata.Add("result", true);
                }
                else
                {
                    resdata.Add("result", false);
                }
            }
            catch
            {
                resdata.Add("result", false);
            }
            return new XmlRpc.XmlRpcResponse
            {
                ReturnValue = resdata
            };
        }

        private string m_DefaultHGRegion;
        private bool GetHGDirectEnabled(UUID regionID)
        {
            bool value;
            if (m_HGDirectEnabled.TryGetValue(regionID, out value) ||
                m_HGDirectEnabled.TryGetValue(UUID.Zero, out value))
            {
                return value;
            }
            return false;
        }

        private readonly RwLockedDictionary<UUID, bool> m_HGDirectEnabled = new RwLockedDictionary<UUID, bool>();

        [ServerParam("DirectHGEnabled")]
        public void DirectHGEnabledUpdated(UUID regionID, string value)
        {
            bool intval;
            if (value.Length == 0)
            {
                m_HGDirectEnabled.Remove(regionID);
            }
            else if (bool.TryParse(value, out intval))
            {
                m_HGDirectEnabled[regionID] = intval;
            }
        }

        [ServerParam("DefaultHGRegion")]
        public void DefaultHGRegion_Updated(UUID regionID, string value)
        {
            m_DefaultHGRegion = value;
        }
    }
}
