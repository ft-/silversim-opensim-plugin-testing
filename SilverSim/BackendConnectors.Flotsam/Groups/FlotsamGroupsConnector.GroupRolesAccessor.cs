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
                var roles = new List<GroupRole>();
                var m = new Map
                {
                    ["GroupID"] = group.ID
                };
                var res = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupRoles", m) as AnArray;
                if(res == null)
                {
                    throw new AccessFailedException();
                }
                foreach(IValue v in res)
                {
                    var data = v as Map;
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
                var m = new Map
                {
                    ["GroupID"] = group.ID,
                    ["AgentID"] = principal.ID
                };
                var res = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
                var rolemems = new List<GroupRole>();
                if (res != null)
                {
                    foreach (IValue v in res)
                    {
                        var data = v as Map;
                        if (data != null)
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
            var m = new Map
            {
                { "GroupID", role.Group.ID },
                { "RoleID", role.ID },
                { "Name", role.Name },
                { "Description", role.Description },
                { "Title", role.Title },
                { "Powers", ((ulong)role.Powers).ToString() }
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.addRoleToGroup", m);
        }

        void IGroupRolesInterface.Update(UUI requestingAgent, GroupRole role)
        {
            var m = new Map
            {
                { "GroupID", role.Group.ID },
                { "RoleID", role.ID },
                { "Name", role.Name },
                { "Description", role.Description },
                { "Title", role.Title },
                { "Powers", ((ulong)role.Powers).ToString() }
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.updateGroupRole", m);
        }

        void IGroupRolesInterface.Delete(UUI requestingAgent, UGI group, UUID roleID)
        {
            var m = new Map
            {
                ["GroupID"] = group.ID,
                ["RoleID"] = roleID
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.removeRoleFromGroup", m);
        }
    }
}
