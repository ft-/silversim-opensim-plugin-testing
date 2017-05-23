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
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        public sealed class GroupRolesAccessor : IGroupRolesInterface
        {
            public int TimeoutMs = 20000;
            private readonly string m_Uri;
            private readonly Func<UUI, string> m_GetGroupsAgentID;

            public GroupRolesAccessor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public bool TryGetValue(UUI requestingAgent, UGI group, UUID roleID, out GroupRole groupRole)
            {
                foreach (GroupRole role in this[requestingAgent, group])
                {
                    if (role.ID.Equals(roleID))
                    {
                        groupRole = role;
                        return true;
                    }
                }
                groupRole = default(GroupRole);
                return false;
            }

            public bool ContainsKey(UUI requestingAgent, UGI group, UUID roleID)
            {
                foreach (GroupRole role in this[requestingAgent, group])
                {
                    if (role.ID.Equals(roleID))
                    {
                        return true;
                    }
                }
                return false;
            }

            public GroupRole this[UUI requestingAgent, UGI group, UUID roleID]
            {
                get
                {
                    GroupRole role;
                    if (!TryGetValue(requestingAgent, group, roleID, out role))
                    {
                        throw new KeyNotFoundException();
                    }
                    return role;
                }
            }

            public List<GroupRole> this[UUI requestingAgent, UGI group]
            {
                get
                {
                    var post = new Dictionary<string, string>
                    {
                        ["GroupID"] = (string)group.ID,
                        ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                        ["METHOD"] = "GETGROUPROLES"
                    };
                    Map m;
                    using(Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                    {
                        m = OpenSimResponse.Deserialize(s);
                    }
                    if (!m.ContainsKey("RESULT"))
                    {
                        return new List<GroupRole>();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        return new List<GroupRole>();
                    }

                    var resultmap = m["RESULT"] as Map;
                    if(resultmap == null)
                    {
                        return new List<GroupRole>();
                    }

                    var roles = new List<GroupRole>();
                    foreach (IValue iv in resultmap.Values)
                    {
                        if (iv is Map)
                        {
                            GroupRole role = iv.ToGroupRole();
                            role.Group = group;
                            roles.Add(role);
                        }
                    }

                    return roles;
                }
            }

            public List<GroupRole> this[UUI requestingAgent, UGI group, UUI principal]
            {
                get
                {
                    var post = new Dictionary<string, string>
                    {
                        ["AgentID"] = m_GetGroupsAgentID(principal),
                        ["GroupID"] = (string)group.ID,
                        ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                        ["METHOD"] = "GETAGENTROLES"
                    };
                    Map m;
                    using(Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                    {
                        m = OpenSimResponse.Deserialize(s);
                    }
                    if (!m.ContainsKey("RESULT"))
                    {
                        return new List<GroupRole>();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        return new List<GroupRole>();
                    }

                    var resultmap = m["RESULT"] as Map;
                    if(resultmap == null)
                    {
                        return new List<GroupRole>();
                    }
                    var roles = new List<GroupRole>();
                    foreach(IValue iv in resultmap.Values)
                    {
                        if(iv is Map)
                        {
                            GroupRole role = iv.ToGroupRole();
                            role.Group = group;
                            roles.Add(role);
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
                var post = new Dictionary<string, string>
                {
                    ["GroupID"] = (string)group.ID,
                    ["RoleID"] = (string)roleID,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "REMOVEROLE"
                };
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }
        }
    }
}
