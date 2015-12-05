// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
        public sealed class ActiveGroupMembershipAccesor : IActiveGroupMembershipInterface
        {
            public int TimeoutMs = 20000;
            readonly string m_Uri;
            readonly Func<UUI, string> m_GetGroupsAgentID;

            public ActiveGroupMembershipAccesor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public bool TryGetValue(UUI requestingAgent, UUI principal, out GroupActiveMembership gam)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETMEMBERSHIP";

                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    gam = default(GroupActiveMembership);
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    gam = default(GroupActiveMembership);
                    return false;
                }

                gam = new GroupActiveMembership();
                Map res = (Map)m["RESULT"];
                gam.User = principal;
                gam.Group = new UGI(res["GroupID"].AsUUID);
                gam.Group.GroupName = res["GroupName"].ToString();
                gam.SelectedRoleID = res["ActiveRole"].AsUUID;
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, UUI principal)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETMEMBERSHIP";

                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    return false;
                }

                return m["RESULT"] is Map;
            }

            public GroupActiveMembership this[UUI requestingAgent, UUI principal]
            {
                get 
                {
                    GroupActiveMembership gam;
                    if(!TryGetValue(requestingAgent, principal, out gam))
                    {
                        throw new KeyNotFoundException();
                    }
                    return gam;
                }
            }
        }
    }
}
