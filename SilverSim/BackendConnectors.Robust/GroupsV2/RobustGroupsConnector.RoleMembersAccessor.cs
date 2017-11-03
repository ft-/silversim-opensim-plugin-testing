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
using System.Linq;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        public sealed class RoleMembersAccessor : IGroupRolemembersInterface
        {
            public int TimeoutMs = 20000;
            private readonly string m_Uri;
            private readonly IGroupMembershipsInterface m_MembershipsAccessor;
            private readonly Func<UUI, string> m_GetGroupsAgentID;

            public RoleMembersAccessor(string uri, IGroupMembershipsInterface membershipsAccessor, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_MembershipsAccessor = membershipsAccessor;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public bool TryGetValue(UUI requestingAgent, UGI group, UUID roleID, UUI principal, out GroupRolemember rolemem)
            {
                foreach (GroupRolemember member in this[requestingAgent, group])
                {
                    if (member.RoleID.Equals(roleID) &&
                        member.Principal.EqualsGrid(principal))
                    {
                        rolemem = member;
                        return true;
                    }
                }
                rolemem = default(GroupRolemember);
                return false;
            }

            public bool ContainsKey(UUI requestingAgent, UGI group, UUID roleID, UUI principal)
            {
                foreach (GroupRolemember member in this[requestingAgent, group])
                {
                    if (member.RoleID.Equals(roleID) &&
                        member.Principal.EqualsGrid(principal))
                    {
                        return true;
                    }
                }
                return false;
            }

            public GroupRolemember this[UUI requestingAgent, UGI group, UUID roleID, UUI principal]
            {
                get
                {
                    GroupRolemember member;
                    if(!TryGetValue(requestingAgent, group, roleID, principal, out member))
                    {
                        throw new KeyNotFoundException();
                    }
                    return member;
                }
            }

            public List<GroupRolemember> this[UUI requestingAgent, UGI group, UUID roleID] =>
                new List<GroupRolemember>(this[requestingAgent, group].Where((member) => member.RoleID.Equals(roleID)));

            public List<GroupRolemember> this[UUI requestingAgent, UGI group]
            {
                get
                {
                    var post = new Dictionary<string, string>
                    {
                        ["GroupID"] = (string)group.ID,
                        ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                        ["METHOD"] = "GETROLEMEMBERS"
                    };
                    Map m;
                    using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
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

                    var resultmap = m["RESULT"] as Map;
                    if(resultmap == null)
                    {
                        throw new KeyNotFoundException();
                    }

                    var rolemembers = new List<GroupRolemember>();
                    foreach (IValue iv in resultmap.Values)
                    {
                        if (iv is Map)
                        {
                            GroupRolemember member = iv.ToGroupRolemember();
                            member.Group = group;
                            rolemembers.Add(member);
                        }
                    }

                    return rolemembers;
                }
            }

            List<GroupRolemembership> GetRolememberships(UUI requestingAgent, UGI group, UUI principal)
            {
                var post = new Dictionary<string, string>
                {
                    ["AgentID"] = m_GetGroupsAgentID(principal),
                    ["GroupID"] = (string)group.ID,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETAGENTROLES"
                };
                Map m;
                using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
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

                var resultmap = m["RESULT"] as Map;
                if(null == resultmap)
                {
                    throw new KeyNotFoundException();
                }

                var rolemembers = new List<GroupRolemembership>();
                foreach (IValue iv in resultmap.Values)
                {
                    var data = iv as Map;
                    if (null != data)
                    {
                        var member = new GroupRolemembership
                        {
                            RoleID = data["RoleID"].AsUUID,
                            Group = group,
                            Principal = principal,
                            GroupTitle = data["Title"].ToString()
                        };
                        rolemembers.Add(member);
                    }
                }

                return rolemembers;

            }

            public List<GroupRolemembership> this[UUI requestingAgent, UUI principal]
            {
                get
                {
                    List<GroupMembership> gmems = m_MembershipsAccessor[requestingAgent, principal];
                    var grm = new List<GroupRolemembership>();
                    foreach(GroupMembership gmem in gmems)
                    {
                        grm.AddRange(GetRolememberships(requestingAgent, gmem.Group, principal));
                    }
                    return grm;
                }
            }

            public void Add(UUI requestingAgent, GroupRolemember rolemember)
            {
                Dictionary<string, string> post = rolemember.ToPost(m_GetGroupsAgentID);
                post["GroupID"] = (string)rolemember.Group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["OP"] = "ADD";
                post["METHOD"] = "AGENTROLE";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }

            public void Delete(UUI requestingAgent, UGI group, UUID roleID, UUI principal)
            {
                var post = new Dictionary<string, string>
                {
                    ["GroupID"] = (string)group.ID,
                    ["RoleID"] = (string)roleID,
                    ["AgentID"] = m_GetGroupsAgentID(principal),
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["OP"] = "DELETE",
                    ["METHOD"] = "AGENTROLE"
                };
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }
        }
    }
}
