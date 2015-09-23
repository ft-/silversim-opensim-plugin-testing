// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Groups;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector
    {
        class GroupRolesAccessor : FlotsamGroupsCommonConnector, IGroupRolesInterface
        {
            public GroupRolesAccessor(string uri)
                : base(uri)
            {
            }

            public GroupRole this[UUI requestingAgent, UGI group, UUID roleID]
            {
                get 
                {
                    foreach(GroupRole role in this[requestingAgent, group])
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
                    List<GroupRole> roles = new List<GroupRole>();
                    Map m = new Map();
                    m.Add("GroupID", group.ID);
                    IValue iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupRoles", m);
                    if(!(iv is AnArray))
                    {
                        throw new AccessFailedException();
                    }
                    foreach(IValue v in (AnArray)iv)
                    {
                        if(v is Map)
                        {
                            roles.Add(v.ToGroupRole(group));
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
                    IValue iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m);
                    List<GroupRole> rolemems = new List<GroupRole>();
                    if (iv is AnArray)
                    {
                        foreach (IValue v in ((AnArray)iv))
                        {
                            if (v is Map)
                            {
                                rolemems.Add(v.ToGroupRole(group));
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
