// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector : GroupsServiceInterface.IGroupRolesInterface
    {
        bool IGroupRolesInterface.TryGetValue(UUI requestingAgent, UGI group, UUID roleID, out GroupRole groupRole)
        {
            foreach (GroupRole role in Roles[requestingAgent, group])
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

        bool IGroupRolesInterface.ContainsKey(UUI requestingAgent, UGI group, UUID roleID)
        {
            foreach (GroupRole role in Roles[requestingAgent, group])
            {
                if (role.ID.Equals(roleID))
                {
                    return true;
                }
            }
            return false;
        }

        GroupRole IGroupRolesInterface.this[UUI requestingAgent, UGI group, UUID roleID]
        {
            get 
            {
                GroupRole role;
                if (!Roles.TryGetValue(requestingAgent, group, roleID, out role))
                {
                    throw new KeyNotFoundException();
                }
                return role;
            }
        }

        List<GroupRole> IGroupRolesInterface.this[UUI requestingAgent, UGI group]
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

        List<GroupRole> IGroupRolesInterface.this[UUI requestingAgent, UGI group, UUI principal]
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

        void IGroupRolesInterface.Add(UUI requestingAgent, GroupRole role)
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

        void IGroupRolesInterface.Update(UUI requestingAgent, GroupRole role)
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

        void IGroupRolesInterface.Delete(UUI requestingAgent, UGI group, UUID roleID)
        {
            Map m = new Map();
            m.Add("GroupID", group.ID);
            m.Add("RoleID", roleID);
            FlotsamXmlRpcCall(requestingAgent, "groups.removeRoleFromGroup", m);
        }
    }
}
