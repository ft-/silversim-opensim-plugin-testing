// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        class InvitesAccessor : IGroupInvitesInterface
        {
            public int TimeoutMs = 20000;
            string m_Uri;
            GetGroupsAgentIDDelegate m_GetGroupsAgentID;

            public InvitesAccessor(string uri, GetGroupsAgentIDDelegate getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public GroupInvite this[UUI requestingAgent, UUID groupInviteID]
            {
                get 
                { 
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["InviteID"] = (string)groupInviteID;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["OP"] = "GET";
                    post["METHOD"] = "INVITE";

                    Map m = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs));
                    if (!m.ContainsKey("RESULT"))
                    {
                        throw new KeyNotFoundException();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        throw new KeyNotFoundException();
                    }

                    Map resultMap = m["RESULT"] as Map;
                    GroupInvite gi = new GroupInvite();
                    gi.ID = resultMap["InviteID"].AsUUID;
                    gi.Group.ID = resultMap["GroupID"].AsUUID;
                    gi.RoleID = resultMap["RoleID"].AsUUID;
                    gi.Principal.ID = resultMap["AgentID"].AsUUID;

                    return gi;
                }
            }

            public List<GroupInvite> this[UUI requestingAgent, UGI group, UUID roleID, UUI principal]
            {
                get 
                {
                    return new List<GroupInvite>();
                }
            }

            public List<GroupInvite> this[UUI requestingAgent, UUI principal]
            {
                get
                {
                    return new List<GroupInvite>();
                }
            }

            public List<GroupInvite> GetByGroup(UUI requestingAgent, UGI group)
            {
                return new List<GroupInvite>();
            }

            public void Add(UUI requestingAgent, GroupInvite invite)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["InviteID"] = (string)invite.ID;
                post["GroupID"] = (string)invite.Group.ID;
                post["RoleID"] = (string)invite.RoleID;
                post["AgentID"] = m_GetGroupsAgentID(invite.Principal);
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["OP"] = "ADD";
                post["METHOD"] = "INVITE";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }

            public void Delete(UUI requestingAgent, UUID inviteID)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["METHOD"] = "INVITE";
                post["OP"] = "DELETE";
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["InviteID"] = (string)inviteID;
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }
        }
    }
}
