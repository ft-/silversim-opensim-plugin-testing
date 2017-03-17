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
                using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
                using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
