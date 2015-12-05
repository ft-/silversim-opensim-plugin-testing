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
using System.Linq;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
        public sealed class RoleMembersAccessor : IGroupRolemembersInterface
        {
            public int TimeoutMs = 20000;
            readonly string m_Uri;
            readonly IGroupMembershipsInterface m_MembershipsAccessor;
            readonly Func<UUI, string> m_GetGroupsAgentID;

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

            public List<GroupRolemember> this[UUI requestingAgent, UGI group, UUID roleID]
            {
                get
                {
                    return new List<GroupRolemember>(this[requestingAgent, group].Where((member) => member.RoleID.Equals(roleID)));
                }
            }

            public List<GroupRolemember> this[UUI requestingAgent, UGI group]
            {
                get
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["GroupID"] = (string)group.ID;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETROLEMEMBERS";
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

                    Map resultmap = m["RESULT"] as Map;
                    if(null == resultmap)
                    {
                        throw new KeyNotFoundException();
                    }

                    List<GroupRolemember> rolemembers = new List<GroupRolemember>();
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
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["GroupID"] = (string)group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETAGENTROLES";
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

                Map resultmap = m["RESULT"] as Map;
                if(null == resultmap)
                {
                    throw new KeyNotFoundException();
                }

                List<GroupRolemembership> rolemembers = new List<GroupRolemembership>();
                foreach (IValue iv in resultmap.Values)
                {
                    Map data = iv as Map;
                    if (null != data)
                    {
                        GroupRolemembership member = new GroupRolemembership();
                        member.RoleID = data["RoleID"].AsUUID;
                        member.Group = group;
                        member.Principal = principal;
                        member.GroupTitle = data["Title"].ToString();
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
                    List<GroupRolemembership> grm = new List<GroupRolemembership>();
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
                Dictionary<string, string> post = new Dictionary<string,string>();
                post["GroupID"] = (string)group.ID;
                post["RoleID"] = (string)roleID;
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["OP"] = "DELETE";
                post["METHOD"] = "AGENTROLE";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }
        }
    }
}
