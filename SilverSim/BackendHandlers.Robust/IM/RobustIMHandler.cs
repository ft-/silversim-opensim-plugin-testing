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
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.Types;
using SilverSim.Types.IM;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SilverSim.BackendConnectors.Robust.IM
{
    #region Service Implementation
    public class RobustIMMessage : GridInstantMessage
    {
        private Func<bool> SignalEvent;
        private readonly object m_Lock = new object();

        private void IMProcessed(GridInstantMessage im, bool result)
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
    public sealed class RobustIMHandler : IPlugin, IServiceURLsGetInterface
    {
        private readonly bool m_DisallowOfflineIM;
        private IMServiceInterface m_IMService;
        private BaseHttpServer m_HttpServer;

        public RobustIMHandler(bool disallowOfflineIM)
        {
            m_DisallowOfflineIM = disallowOfflineIM;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_HttpServer = loader.HttpServer;
            HttpXmlRpcHandler xmlRpc = loader.GetService<HttpXmlRpcHandler>("XmlRpcServer");
            xmlRpc.XmlRpcMethods.Add("grid_instant_message", IMReceived);
            m_IMService = loader.GetService<IMServiceInterface>("IMService");
        }

        void IServiceURLsGetInterface.GetServiceURLs(Dictionary<string, string> dict)
        {
            dict["IMServerURI"] = m_HttpServer.ServerURI;
        }

        private XmlRpc.XmlRpcResponse IMReceived(XmlRpc.XmlRpcRequest req)
        {
            var im = new RobustIMMessage();
            var res = new XmlRpc.XmlRpcResponse();
            try
            {
                im.NoOfflineIMStore = m_DisallowOfflineIM;
                var d = (Map)req.Params[0];

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

            var p = new Map();
            using (var e = new ManualResetEvent(false))
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
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection) =>
            new RobustIMHandler(ownSection.GetBoolean("DisallowOfflineIM", true));
    }
    #endregion
}
