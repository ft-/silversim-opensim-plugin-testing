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
using System.Linq;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector : GroupsServiceInterface.IGroupRolemembersInterface
    {
        bool IGroupRolemembersInterface.TryGetValue(UUI requestingAgent, UGI group, UUID roleID, UUI principal, out GroupRolemember role)
        {
            var m = new Map
            {
                ["GroupID"] = group.ID,
                ["AgentID"] = principal.ID
            };
            var iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
            if (iv != null)
            {
                foreach (IValue v in iv)
                {
                    m = v as Map;
                    if (m != null)
                    {
                        GroupRolemember gmem = m.ToGroupRolemember(group, m_AvatarNameService);
                        if (gmem.RoleID.Equals(roleID))
                        {
                            role = gmem;
                            return true;
                        }
                    }
                }
            }

            role = default(GroupRolemember);
            return false;
        }

        bool IGroupRolemembersInterface.ContainsKey(UUI requestingAgent, UGI group, UUID roleID, UUI principal)
        {
            var m = new Map
            {
                ["GroupID"] = group.ID,
                ["AgentID"] = principal.ID
            };
            var iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
            if (iv != null)
            {
                foreach (IValue v in iv)
                {
                    m = v as Map;
                    if (m != null)
                    {
                        GroupRolemember gmem = m.ToGroupRolemember(group, m_AvatarNameService);
                        if (gmem.RoleID.Equals(roleID))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        GroupRolemember IGroupRolemembersInterface.this[UUI requestingAgent, UGI group, UUID roleID, UUI principal]
        {
            get
            {
                var m = new Map
                {
                    ["GroupID"] = group.ID,
                    ["AgentID"] = principal.ID
                };
                var iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
                if(iv != null)
                {
                    foreach (IValue v in iv)
                    {
                        m = v as Map;
                        if (m != null)
                        {
                            GroupRolemember gmem = m.ToGroupRolemember(group, m_AvatarNameService);
                            if(gmem.RoleID.Equals(roleID))
                            {
                                return gmem;
                            }
                        }
                    }
                }

                throw new KeyNotFoundException();
            }
        }

        List<GroupRolemembership> IGroupRolemembersInterface.this[UUI requestingAgent, UUI principal]
        {
            get
            {
                var gmems = new List<GroupRolemembership>();
                var m = new Map
                {
                    ["AgentID"] = principal.ID
                };
                var iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
                if(iv != null)
                {
                    foreach (IValue v in iv)
                    {
                        var data = v as Map;
                        if(data != null)
                        {
                            GroupRolemembership gmem = data.ToGroupRolemembership(m_AvatarNameService);
                            gmems.Add(gmem);
                        }
                    }
                }
                return gmems;
            }
        }

        List<GroupRolemember> IGroupRolemembersInterface.this[UUI requestingAgent, UGI group, UUID roleID] =>
            new List<GroupRolemember>(Rolemembers[requestingAgent, group].Where(g => g.RoleID.Equals(roleID)));

        List<GroupRolemember> IGroupRolemembersInterface.this[UUI requestingAgent, UGI group]
        {
            get
            {
                var m = new Map
                {
                    ["GroupID"] = group.ID
                };
                var iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupRoleMembers", m) as AnArray;
                var rolemems = new List<GroupRolemember>();
                if(iv != null)
                {
                    foreach(IValue v in iv)
                    {
                        m = v as Map;
                        if(m != null)
                        {
                            rolemems.Add(m.ToGroupRolemember(group, m_AvatarNameService));
                        }
                    }
                }
                return rolemems;
            }
        }

        void IGroupRolemembersInterface.Add(UUI requestingAgent, GroupRolemember rolemember)
        {
            var m = new Map
            {
                ["AgentID"] = rolemember.Principal.ID,
                ["GroupID"] = rolemember.Group.ID,
                ["RoleID"] = rolemember.RoleID
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.addAgentToGroupRole", m);
        }

        void IGroupRolemembersInterface.Delete(UUI requestingAgent, UGI group, UUID roleID, UUI principal)
        {
            var m = new Map
            {
                ["AgentID"] = principal.ID,
                ["GroupID"] = group.ID,
                ["RoleID"] = roleID
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.removeAgentFromGroupRole", m);
        }
    }
}
