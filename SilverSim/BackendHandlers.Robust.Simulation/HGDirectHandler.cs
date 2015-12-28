﻿// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.ComponentModel;

namespace SilverSim.BackendHandlers.Robust.Simulation
{
    #region Service Implementation
    [Description("OpenSim PostAgent Direct HG Handler")]
    public class PostAgentHGDirectHandler : PostAgentHandler
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
            if (!m_ServerParams.GetBoolean(sceneID, "HGDirectEnabled"))
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
                region_name = m_ServerParams.GetString(UUID.Zero, "DefaultHGRegion", region_name);
            }

            if(!string.IsNullOrEmpty(region_name))
            {
                try
                {
                    SceneInterface s = Scene.Management.Scene.SceneManager.Scenes[region_name];
                    if (m_ServerParams.GetBoolean(s.ID, "HGDirectEnabled"))
                    {
                        resdata.Add("uuid", s.ID);
                        resdata.Add("handle", s.RegionData.Location.RegionHandle.ToString());
                        resdata.Add("region_image", string.Empty);
                        resdata.Add("external_name", s.RegionData.ServerURI + " " + s.RegionData.Name);
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
                SceneInterface s = Scene.Management.Scene.SceneManager.Scenes[region_uuid];
                if (m_ServerParams.GetBoolean(s.ID, "HGDirectEnabled"))
                {
                    resdata.Add("uuid", s.ID);
                    resdata.Add("handle", s.RegionData.Location.RegionHandle.ToString());
                    resdata.Add("x", s.RegionData.Location.X);
                    resdata.Add("y", s.RegionData.Location.Y);
                    resdata.Add("region_name", s.RegionData.Name);
                    Uri serverURI = new Uri(s.RegionData.ServerURI);
                    resdata.Add("hostname", serverURI.Host);
                    resdata.Add("http_port", s.RegionData.ServerHttpPort);
                    resdata.Add("internal_port", s.RegionData.ServerPort);
                    resdata.Add("server_uri", s.RegionData.ServerURI);
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
