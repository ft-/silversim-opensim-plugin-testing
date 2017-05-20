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
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Types;
using SilverSim.Types.Presence;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace SilverSim.BackendConnectors.Robust.Presence
{
    [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
    [Description("Robust HGOnly Presence Connector")]
    public class RobustHGOnlyPresenceConnector : PresenceServiceInterface, IPlugin
    {
        public int TimeoutMs { get; set; }
        private readonly string m_HomeURI;

        #region Constructor
        public RobustHGOnlyPresenceConnector(string homeuri)
        {
            TimeoutMs = 20000;
            m_HomeURI = homeuri;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        public override List<PresenceInfo> GetPresencesInRegion(UUID regionId)
        {
            throw new NotSupportedException("GetPresencesInRegion");
        }

        public override void Remove(UUID scopeID, UUID accountID)
        {
            throw new NotSupportedException("Remove");
        }

        private void HGLogout(UUID sessionID, UUID userId)
        {
            var p = new Map
            {
                ["userID"] = userId,
                ["sessionID"] = sessionID
            };
            var req = new XmlRpc.XmlRpcRequest("logout_agent");
            req.Params.Add(p);
            XmlRpc.XmlRpcResponse res;
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
                var d = (Map)res.ReturnValue;
                if (bool.Parse(d["result"].ToString()))
                {
                    return;
                }
            }
            throw new PresenceUpdateFailedException();
        }

        public override List<PresenceInfo> this[UUID userID] => new List<PresenceInfo>();

        public override PresenceInfo this[UUID sessionID, UUID userID]
        {
            get { throw new NotSupportedException(); }

            set
            {
                if (value == null)
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
                else if(reportType == SetType.Login || reportType == SetType.Report)
                {
                    /* no action needed */
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
