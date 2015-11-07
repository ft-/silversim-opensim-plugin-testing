﻿// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Types;
using SilverSim.Types.Presence;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SilverSim.BackendConnectors.Robust.Presence
{
    [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
    public class RobustHGPresenceConnector : PresenceServiceInterface
    {
        public int TimeoutMs { get; set; }
        readonly string m_HomeURI;
        readonly RobustPresenceConnector m_LocalConnector;

        #region Constructor
        public RobustHGPresenceConnector(string uri, string homeuri)
        {
            TimeoutMs = 20000;
            m_LocalConnector = new RobustPresenceConnector(uri, homeuri);
            m_HomeURI = homeuri;
        }
        #endregion

        void HGLogout(UUID sessionID, UUID userId)
        {
            Map p = new Map();
            p.Add("userID", userId);
            p.Add("sessionID", sessionID);

            XmlRpc.XmlRpcRequest req = new XmlRpc.XmlRpcRequest("logout_agent");
            req.Params.Add(p);
            XmlRpc.XmlRpcResponse res;
            try
            {
                res = RPC.DoXmlRpcRequest(m_HomeURI, req, TimeoutMs);
            }
            catch (XmlRpc.XmlRpcFaultException)
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
                return m_LocalConnector[userID];
            }
        }

        public override PresenceInfo this[UUID sessionID, UUID userID]
        {
            get
            {
                return m_LocalConnector[sessionID, userID];
            }
            set
            {
                if(value == null)
                {
                    try
                    {
                        m_LocalConnector[sessionID, userID] = null;
                    }
                    catch
                    {
                        /* no action needed */
                    }
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
                    try
                    {
                        m_LocalConnector[sessionID, userID, reportType] = null;
                    }
                    catch
                    {
                        /* no action needed */
                    }
                    HGLogout(sessionID, userID);
                }
                else if(reportType == SetType.Login)
                {
                    /* no action needed */
                }
                else if(reportType == SetType.Report)
                {
                    m_LocalConnector[sessionID, userID, reportType] = value;
                }
                else
                {
                    throw new ArgumentException("Invalid reportType specified");
                }
            }
        }

        public override void LogoutRegion(UUID regionID)
        {
            /* no action needed */
        }
    }
}
