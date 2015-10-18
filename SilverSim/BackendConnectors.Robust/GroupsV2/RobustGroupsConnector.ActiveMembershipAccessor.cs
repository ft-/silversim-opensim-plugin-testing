// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        public sealed class ActiveGroupMembershipAccesor : IActiveGroupMembershipInterface
        {
            public int TimeoutMs = 20000;
            string m_Uri;
            Func<UUI, string> m_GetGroupsAgentID;

            public ActiveGroupMembershipAccesor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public GroupActiveMembership this[UUI requestingAgent, UUI principal]
            {
                get 
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETMEMBERSHIP";

                    Map m;
                    using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                    {
                        m = OpenSimResponse.Deserialize(s);
                    }
                    if (!m.ContainsKey("RESULT"))
                    {
                        throw new KeyNotFoundException();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        throw new KeyNotFoundException();
                    }

                    GroupActiveMembership gam = new GroupActiveMembership();
                    Map res = (Map)m["RESULT"];
                    gam.User = principal;
                    gam.Group = new UGI(res["GroupID"].AsUUID);
                    gam.Group.GroupName = res["GroupName"].ToString();
                    gam.SelectedRoleID = res["ActiveRole"].AsUUID;
                    return gam;
                }
            }
        }
    }
}
