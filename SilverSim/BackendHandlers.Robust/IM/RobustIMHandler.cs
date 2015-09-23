// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.Types;
using SilverSim.Types.IM;
using SilverSim.Types.StructuredData.XMLRPC;
using System;
using System.Threading;

namespace SilverSim.BackendConnectors.Robust.IM
{
    #region Service Implementation
    class RobustIMHandler : IPlugin
    {
        bool m_DisallowOfflineIM;
        IMServiceInterface m_IMService;
        public RobustIMHandler(bool disallowOfflineIM)
        {
            m_DisallowOfflineIM = disallowOfflineIM;
        }

        public void Startup(ConfigurationLoader loader)
        {
            HttpXmlRpcHandler xmlRpc = loader.GetService<HttpXmlRpcHandler>("XmlRpcServer");
            xmlRpc.XmlRpcMethods.Add("grid_instant_message", IMReceived);
            m_IMService = loader.GetService<IMServiceInterface>("IMService");
        }

        public XMLRPC.XmlRpcResponse IMReceived(XMLRPC.XmlRpcRequest req)
        {
            GridInstantMessage im = new GridInstantMessage();
            XMLRPC.XmlRpcResponse res = new XMLRPC.XmlRpcResponse();
            try
            {
                im.NoOfflineIMStore = m_DisallowOfflineIM;
                Map d = (Map)req.Params[0];

                im.FromAgent.ID = d["from_agent_id"].AsUUID;
                im.FromGroup.ID = d["from_agent_id"].AsUUID;
                im.ToAgent.ID = d["to_agent_id"].ToString();
                im.IMSessionID = d["im_session_id"].AsUUID;
                im.RegionID = d["region_id"].AsUUID;
                im.Timestamp = Date.UnixTimeToDateTime(d["timestamp"].AsULong);
                im.FromAgent.FullName = d["from_agent_name"].ToString();
                if(d.ContainsKey("message"))
                {
                    im.Message = d["message"].ToString();
                }
                byte[] dialog = Convert.FromBase64String(d["dialog"].ToString());
                im.Dialog = (GridInstantMessageDialog)dialog[0];
                im.IsFromGroup = bool.Parse(d["from_group"].ToString());
                byte[] offline = Convert.FromBase64String(d["offline"].ToString());
                im.IsOffline = offline[0] != 0;
                im.ParentEstateID = d["parent_estate_id"].AsUInt;
                im.Position.X = float.Parse(d["position_x"].ToString());
                im.Position.Y = float.Parse(d["position_y"].ToString());
                im.Position.Z = float.Parse(d["position_z"].ToString());
                if(d.ContainsKey("binary_bucket"))
                {
                    im.BinaryBucket = Convert.FromBase64String(d["binary_bucket"].ToString());
                }
            }
            catch
            {
                throw new XMLRPC.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            ManualResetEvent e = new ManualResetEvent(false);
            Map p;
            im.OnResult = delegate(GridInstantMessage _im, bool result) { _im.ResultInfo = result;  e.Set(); };
            m_IMService.Send(im);
            try
            {
                e.WaitOne(15000);
            }
            catch
            {
                p = new Map();
                p.Add("result", "FALSE");
                res.ReturnValue = p;
                return res;
            }

            p = new Map();
            p.Add("result", im.ResultInfo ? "TRUE" : "FALSE");
            res.ReturnValue = p;

            return res;
        }
    }
    #endregion

    #region Factory
    [PluginName("IMHandler")]
    public class RobustIMHandlerFactory : IPluginFactory
    {
        public RobustIMHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustIMHandler(ownSection.GetBoolean("DisallowOfflineIM", true));
        }
    }
    #endregion
}
