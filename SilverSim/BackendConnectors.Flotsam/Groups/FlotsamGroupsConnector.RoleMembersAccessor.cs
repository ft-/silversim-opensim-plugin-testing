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
            Map m = new Map();
            m.Add("GroupID", group.ID);
            m.Add("AgentID", principal.ID);
            AnArray iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
            if (null != iv)
            {
                foreach (IValue v in iv)
                {
                    m = v as Map;
                    if (null != m)
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
            Map m = new Map();
            m.Add("GroupID", group.ID);
            m.Add("AgentID", principal.ID);
            AnArray iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
            if (null != iv)
            {
                foreach (IValue v in iv)
                {
                    m = v as Map;
                    if (null != m)
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
                Map m = new Map();
                m.Add("GroupID", group.ID);
                m.Add("AgentID", principal.ID);
                AnArray iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
                if(null != iv)
                {
                    foreach (IValue v in iv)
                    {
                        m = v as Map;
                        if (null != m)
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
                List<GroupRolemembership> gmems = new List<GroupRolemembership>();
                Map m = new Map();
                m.Add("AgentID", principal.ID);
                AnArray iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentRoles", m) as AnArray;
                if(null != iv)
                {
                    foreach (IValue v in iv)
                    {
                        Map data = v as Map;
                        if(null != data)
                        {
                            GroupRolemembership gmem = data.ToGroupRolemembership(m_AvatarNameService);
                            gmems.Add(gmem);
                        }
                    }
                }
                return gmems;
            }
        }

        List<GroupRolemember> IGroupRolemembersInterface.this[UUI requestingAgent, UGI group, UUID roleID]
        {
            get 
            {
                return new List<GroupRolemember>(Rolemembers[requestingAgent, group].Where(g => g.RoleID.Equals(roleID)));
            }
        }

        List<GroupRolemember> IGroupRolemembersInterface.this[UUI requestingAgent, UGI group]
        {
            get
            {
                Map m = new Map();
                m.Add("GroupID", group.ID);
                AnArray iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupRoleMembers", m) as AnArray;
                List<GroupRolemember> rolemems = new List<GroupRolemember>();
                if(iv is AnArray)
                {
                    foreach(IValue v in iv)
                    {
                        m = v as Map;
                        if(null != m)
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
            Map m = new Map();
            m.Add("AgentID", rolemember.Principal.ID);
            m.Add("GroupID", rolemember.Group.ID);
            m.Add("RoleID", rolemember.RoleID);
            FlotsamXmlRpcCall(requestingAgent, "groups.addAgentToGroupRole", m);
        }

        void IGroupRolemembersInterface.Delete(UUI requestingAgent, UGI group, UUID roleID, UUI principal)
        {
            Map m = new Map();
            m.Add("AgentID", principal.ID);
            m.Add("GroupID", group.ID);
            m.Add("RoleID", roleID);
            FlotsamXmlRpcCall(requestingAgent, "groups.removeAgentFromGroupRole", m);
        }
    }
}
