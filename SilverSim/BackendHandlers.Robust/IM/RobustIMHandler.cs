// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.Types;
using SilverSim.Types.IM;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.ComponentModel;
using System.Threading;

namespace SilverSim.BackendConnectors.Robust.IM
{
    #region Service Implementation
    public class RobustIMMessage : GridInstantMessage
    {
        Func<bool> SignalEvent;
        readonly object m_Lock = new object();

        void IMProcessed(GridInstantMessage im, bool result)
        {
            lock(m_Lock)
            {
                im.ResultInfo = result;
                if(SignalEvent != null)
                {
                    SignalEvent();
                }
            }
        }

        internal void ActivateIMEvent(Func<bool> e)
        {
            lock(m_Lock)
            {
                SignalEvent = e;
            }
        }

        internal void DeactivateIMEvent()
        {
            lock(m_Lock)
            {
                SignalEvent = null;
            }
        }

        internal RobustIMMessage()
        {
            OnResult = IMProcessed;
        }
    }

    [Description("OpenSim InstantMessage Server")]
    public sealed class RobustIMHandler : IPlugin
    {
        readonly bool m_DisallowOfflineIM;
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

        public XmlRpc.XmlRpcResponse IMReceived(XmlRpc.XmlRpcRequest req)
        {
            RobustIMMessage im = new RobustIMMessage();
            XmlRpc.XmlRpcResponse res = new XmlRpc.XmlRpcResponse();
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
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            Map p = new Map();
            using (ManualResetEvent e = new ManualResetEvent(false))
            {
                im.ActivateIMEvent(e.Set);
                m_IMService.Send(im);
                try
                {
                    e.WaitOne(15000);
                }
                catch
                {
                    p.Add("result", "FALSE");
                    res.ReturnValue = p;
                    return res;
                }
                finally
                {
                    im.DeactivateIMEvent();
                }
            }

            
            p.Add("result", im.ResultInfo ? "TRUE" : "FALSE");
            res.ReturnValue = p;

            return res;
        }
    }
    #endregion

    #region Factory
    [PluginName("IMHandler")]
    public sealed class RobustIMHandlerFactory : IPluginFactory
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
