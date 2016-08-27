// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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
    #region Service Implementation
    [Description("OpenSim PostAgent Direct HG Handler")]
    [ServerParam("DirectHGEnabled")]
    [ServerParam("DefaultHGRegion")]
    public class PostAgentHGDirectHandler : PostAgentHandler, IServerParamListener
    {
        private HttpXmlRpcHandler m_XmlRpcServer;

        public PostAgentHGDirectHandler(IConfig ownSection)
            : base("/foreignagent/", ownSection)
        {

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

        XmlRpc.XmlRpcResponse LinkRegion(XmlRpc.XmlRpcRequest req)
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
                    SceneInterface s = m_Scenes[region_name];
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
            XmlRpc.XmlRpcResponse res = new XmlRpc.XmlRpcResponse();
            res.ReturnValue = resdata;
            return res;
        }

        XmlRpc.XmlRpcResponse GetRegion(XmlRpc.XmlRpcRequest req)
        {
            UUID region_uuid = (((Map)req.Params[0])["region_uuid"]).AsUUID;
            Map resdata = new Map();
            try
            {
                SceneInterface s = m_Scenes[region_uuid];
                if (GetHGDirectEnabled(s.ID))
                {
                    resdata.Add("uuid", s.ID);
                    resdata.Add("handle", s.GridPosition.RegionHandle.ToString());
                    resdata.Add("x", s.GridPosition.X);
                    resdata.Add("y", s.GridPosition.Y);
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
            XmlRpc.XmlRpcResponse res = new XmlRpc.XmlRpcResponse();
            res.ReturnValue = resdata;
            return res;
        }

        string m_DefaultHGRegion;
        bool GetHGDirectEnabled(UUID regionID)
        {
            bool value;
            if (m_HGDirectEnabled.TryGetValue(regionID, out value) ||
                m_HGDirectEnabled.TryGetValue(UUID.Zero, out value))
            {
                return value;
            }
            return false;
        }

        readonly RwLockedDictionary<UUID, bool> m_HGDirectEnabled = new RwLockedDictionary<UUID, bool>();

        [ServerParam("DirectHGEnabled")]
        public void DirectHGEnabledUpdated(UUID regionID, string parametername, string value)
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
    #endregion

    #region Service Factory
    [PluginName("RobustDirectHGHandler")]
    public class PostAgentDirectHGHandlerFactory : IPluginFactory
    {
        public PostAgentDirectHGHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new PostAgentHGDirectHandler(ownSection);
        }
    }
    #endregion

}
