// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.HttpClient;
using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.Types;
using SilverSim.Types.IM;
using SilverSim.Types.StructuredData.XMLRPC;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.IM
{
    public class RobustIMConnector : IMServiceInterface
    {
        public int TimeoutMs { get; set; }

        string m_IMUri;

        public RobustIMConnector(string uri)
        {
            TimeoutMs = 20000;
            m_IMUri = uri;
        }

        public override void Send(GridInstantMessage im)
        {
            XMLRPC.XmlRpcRequest req = new XMLRPC.XmlRpcRequest();
            
            Map p = new Map();
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
            byte[] v = new byte[1];
            v[0] = (byte)(int)im.Dialog;
            p.Add("dialog", Convert.ToBase64String(v, Base64FormattingOptions.None));
            v = new byte[1];
            v[0] = (im.IsOffline ? (byte)1 : (byte)0);
            p.Add("offline", Convert.ToBase64String(v, Base64FormattingOptions.None));
            p.Add("parent_estate_id", im.ParentEstateID.ToString());
            p.Add("position_x", im.Position.X.ToString());
            p.Add("position_y", im.Position.Y.ToString());
            p.Add("position_z", im.Position.Z.ToString());
            p.Add("binary_bucket", Convert.ToBase64String(im.BinaryBucket, Base64FormattingOptions.None));

            req.MethodName = "grid_instant_message";
            req.Params.Add(p);

            XMLRPC.XmlRpcResponse res;
            try
            {
                res = RPC.DoXmlRpcRequest(m_IMUri, req, TimeoutMs);
            }
            catch(XMLRPC.XmlRpcFaultException)
            {
                throw new IMSendFailedException();
            }
            if(res.ReturnValue is Map)
            {
                Map d = (Map)res.ReturnValue;
                if(bool.Parse(d["success"].ToString()))
                {
                    return;
                }
            }
            throw new IMSendFailedException();
        }
    }
}
