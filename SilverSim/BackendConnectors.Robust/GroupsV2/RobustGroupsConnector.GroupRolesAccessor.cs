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
        class GroupRolesAccessor : IGroupRolesInterface
        {
            public int TimeoutMs = 20000;
            string m_Uri;
            GetGroupsAgentIDDelegate m_GetGroupsAgentID;

            public GroupRolesAccessor(string uri, GetGroupsAgentIDDelegate getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public GroupRole this[UUI requestingAgent, UGI group, UUID roleID]
            {
                get 
                {
                    List<GroupRole> roles = this[requestingAgent, group];
                    foreach(GroupRole role in roles)
                    {
                        if(role.ID.Equals(roleID))
                        {
                            return role;
                        }
                    }
                    throw new KeyNotFoundException();
                }
            }

            public List<GroupRole> this[UUI requestingAgent, UGI group]
            {
                get
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["GroupID"] = (string)group.ID;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETGROUPROLES";
                    Map m = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs));
                    if (!m.ContainsKey("RESULT"))
                    {
                        return new List<GroupRole>();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        return new List<GroupRole>();
                    }

                    List<GroupRole> roles = new List<GroupRole>();
                    foreach (IValue iv in ((Map)m["RESULT"]).Values)
                    {
                        if (iv is Map)
                        {
                            roles.Add(iv.ToGroupRole());
                        }
                    }

                    return roles;
                }
            }

            public List<GroupRole> this[UUI requestingAgent, UGI group, UUI principal]
            {
                get
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["GroupID"] = (string)group.ID;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETAGENTROLES";
                    Map m = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs));
                    if (!m.ContainsKey("RESULT"))
                    {
                        return new List<GroupRole>();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        return new List<GroupRole>();
                    }

                    List<GroupRole> roles = new List<GroupRole>();
                    foreach(IValue iv in ((Map)m["RESULT"]).Values)
                    {
                        if(iv is Map)
                        {
                            roles.Add(iv.ToGroupRole());
                        }
                    }

                    return roles;
                }
            }

            public void Add(UUI requestingAgent, GroupRole role)
            {
                Dictionary<string, string> post = role.ToPost();
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["OP"] = "ADD";
                post["METHOD"] = "PUTROLE";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }

            public void Update(UUI requestingAgent, GroupRole role)
            {
                Dictionary<string, string> post = role.ToPost();
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["OP"] = "UPDATE";
                post["METHOD"] = "PUTROLE";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }

            public void Delete(UUI requestingAgent, UGI group, UUID roleID)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["GroupID"] = (string)group.ID;
                post["RoleID"] = (string)roleID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "REMOVEROLE";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }
        }
    }
}
