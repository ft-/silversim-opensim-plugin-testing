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

using SilverSim.Main.Common;
using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.Types;
using SilverSim.Types.IM;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.ComponentModel;

namespace SilverSim.BackendConnectors.Robust.IM
{
    [Description("OpenSim IM Connector")]
    public class RobustIMConnector : IMServiceInterface, IPlugin
    {
        public int TimeoutMs { get; set; }

        private readonly string m_IMUri;

        public RobustIMConnector(string uri)
        {
            TimeoutMs = 20000;
            m_IMUri = uri;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public override void Send(GridInstantMessage im)
        {
            var req = new XmlRpc.XmlRpcRequest();

            var p = new Map();
            if (im.IsFromGroup)
            {
                p.Add("from_agent_id", im.FromGroup.ID);
            }
            else
            {
                p.Add("from_agent_id", im.FromAgent.ID);
            }
            p.Add("from_agent_session", UUID.Zero);
            p.Add("to_agent_id", im.ToAgent.ID);
            p.Add("im_session_id", im.IMSessionID);
            p.Add("region_id", im.RegionID);
            p.Add("timestamp", im.Timestamp.AsULong.ToString());
            p.Add("from_agent_name", im.FromAgent.FullName);
            p.Add("from_group", im.IsFromGroup ? "TRUE" : "FALSE");
            p.Add("message", im.Message);
            var v = new byte[1];
            v[0] = (byte)(int)im.Dialog;
            p.Add("dialog", Convert.ToBase64String(v, Base64FormattingOptions.None));
            v = new byte[1];
            v[0] = im.IsOffline ? (byte)1 : (byte)0;
            p.Add("offline", Convert.ToBase64String(v, Base64FormattingOptions.None));
            p.Add("parent_estate_id", im.ParentEstateID.ToString());
            p.Add("position_x", im.Position.X.ToString());
            p.Add("position_y", im.Position.Y.ToString());
            p.Add("position_z", im.Position.Z.ToString());
            p.Add("binary_bucket", Convert.ToBase64String(im.BinaryBucket, Base64FormattingOptions.None));

            req.MethodName = "grid_instant_message";
            req.Params.Add(p);

            XmlRpc.XmlRpcResponse res;
            try
            {
                res = RPC.DoXmlRpcRequest(m_IMUri, req, TimeoutMs);
            }
            catch (XmlRpc.XmlRpcFaultException)
            {
                throw new IMSendFailedException();
            }
            if(res.ReturnValue is Map)
            {
                var d = (Map)res.ReturnValue;
                if(bool.Parse(d["success"].ToString()))
                {
                    return;
                }
            }
            throw new IMSendFailedException();
        }
    }
}
