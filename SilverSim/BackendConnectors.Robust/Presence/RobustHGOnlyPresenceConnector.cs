// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Types;
using SilverSim.Types.Presence;
using SilverSim.Types.StructuredData.XMLRPC;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.Presence
{
    public class RobustHGOnlyPresenceConnector : PresenceServiceInterface
    {
        public int TimeoutMs { get; set; }
        string m_HomeURI;

        #region Constructor
        public RobustHGOnlyPresenceConnector(string homeuri)
        {
            TimeoutMs = 20000;
            m_HomeURI = homeuri;
        }
        #endregion

        void HGLogout(UUID sessionID, UUID userId)
        {
            Map p = new Map();
            p.Add("userID", userId);
            p.Add("sessionID", sessionID);

            XMLRPC.XmlRpcRequest req = new XMLRPC.XmlRpcRequest("logout_agent");
            req.Params.Add(p);
            XMLRPC.XmlRpcResponse res;
            try
            {
                res = RPC.DoXmlRpcRequest(m_HomeURI, req, TimeoutMs);
            }
            catch
            {
                throw new PresenceUpdateFailedException();
            }
            if (res.ReturnValue is Map)
            {
                Map d = (Map)res.ReturnValue;
                if (bool.Parse(d["result"].ToString()))
                {
                    return;
                }
            }
            throw new PresenceUpdateFailedException();
        }

        public override List<PresenceInfo> this[UUID userID]
        {
            get
            {
                return new List<PresenceInfo>();
            }
        }

        public override PresenceInfo this[UUID sessionID, UUID userID]
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                if(value == null)
                {
                    HGLogout(sessionID, userID);
                }
                else
                {
                    throw new ArgumentException("setting value != null is not allowed without reportType");
                }
            }
        }

        public override PresenceInfo this[UUID sessionID, UUID userID, SetType reportType]
        {
            set
            {
                if (value == null)
                {
                    HGLogout(sessionID, userID);
                }
                else if(reportType == SetType.Login)
                {
                }
                else if(reportType == SetType.Report)
                {
                }
                else
                {
                    throw new ArgumentException("Invalid reportType specified");
                }
            }
        }

        public override void logoutRegion(UUID regionID)
        {
        }
    }
}
