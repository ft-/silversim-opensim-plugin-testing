// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Groups;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector
    {
        public sealed class GroupRolesAccessor : FlotsamGroupsCommonConnector, IGroupRolesInterface
        {
            public GroupRolesAccessor(string uri)
                : base(uri)
            {
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
                    List<GroupRole> roles = new List<GroupRole>();
                    Map m = new Map();
                    m.Add("GroupID", group.ID);
                    AnArray res = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupRoles", m) as AnArray;
                    if(null == res)
                    {
                        throw new AccessFailedException();
                    }
                    foreach(IValue v in res)
                    {
                        Map data = v as Map;
                        if(data != null)
                        {
                            roles.Add(data.ToGroupRole(group));
                        }
                    }
                    return roles;
                }
            }

            public List<GroupRole> this[UUI requestingAgent, UGI group, UUI principal]
            {
                get 
                {
                    Map m = new Map();
                    m.Add("GroupID", group.ID);
                    m.Add("AgentID", principal.ID); 
                    AnArray res = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
                    List<GroupRole> rolemems = new List<GroupRole>();
                    if (null != res)
                    {
                        foreach (IValue v in res)
                        {
                            Map data = v as Map;
                            if (null != data)
                            {
                                rolemems.Add(data.ToGroupRole(group));
                            }
                        }
                    }
                    return rolemems;
                }
            }

            public void Add(UUI requestingAgent, GroupRole role)
            {
                Map m = new Map();
                m.Add("GroupID", role.Group.ID);
                m.Add("RoleID", role.ID);
                m.Add("Name", role.Name);
                m.Add("Description", role.Description);
                m.Add("Title", role.Title);
                m.Add("Powers", ((ulong)role.Powers).ToString());
                FlotsamXmlRpcCall(requestingAgent, "groups.addRoleToGroup", m);
            }

            public void Update(UUI requestingAgent, GroupRole role)
            {
                Map m = new Map();
                m.Add("GroupID", role.Group.ID);
                m.Add("RoleID", role.ID);
                m.Add("Name", role.Name);
                m.Add("Description", role.Description);
                m.Add("Title", role.Title);
                m.Add("Powers", ((ulong)role.Powers).ToString());
                FlotsamXmlRpcCall(requestingAgent, "groups.updateGroupRole", m);
            }

            public void Delete(UUI requestingAgent, UGI group, UUID roleID)
            {
                Map m = new Map();
                m.Add("GroupID", group.ID);
                m.Add("RoleID", roleID);
                FlotsamXmlRpcCall(requestingAgent, "groups.removeRoleFromGroup", m);
            }
        }
    }
}
