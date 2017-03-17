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
using System;
using System.Collections.Generic;
using System.Linq;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector : GroupsServiceInterface.IGroupMembersInterface
    {
        bool IGroupMembersInterface.TryGetValue(UUI requestingAgent, UGI group, UUI principal, out GroupMember gmem)
        {
            IEnumerable<GroupMember> e = Members[requestingAgent, group].Where(p => p.Principal.ID == principal.ID);
            foreach (GroupMember g in e)
            {
                gmem = g;
                return true;
            }
            gmem = default(GroupMember);
            return false;
        }

        bool IGroupMembersInterface.ContainsKey(UUI requestingAgent, UGI group, UUI principal)
        {
            IEnumerable<GroupMember> e = Members[requestingAgent, group].Where(p => p.Principal.ID == principal.ID);
            foreach (GroupMember g in e)
            {
                return true;
            }
            return false;
        }

        GroupMember IGroupMembersInterface.this[UUI requestingAgent, UGI group, UUI principal]
        {
            get 
            {
                IEnumerable<GroupMember> e = Members[requestingAgent, group].Where(p => p.Principal.ID == principal.ID);
                foreach(GroupMember g in e)
                {
                    return g;
                }
                throw new KeyNotFoundException();
            }
        }

        List<GroupMember> IGroupMembersInterface.this[UUI requestingAgent, UGI group]
        {
            get 
            {
                Map m = new Map();
                m.Add("GroupID", group.ID);
                AnArray res = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupMembers", m) as AnArray;
                if(null == res)
                {
                    throw new AccessFailedException();
                }
                List<GroupMember> gmems = new List<GroupMember>();
                foreach (IValue iv in res)
                {
                    Map data = iv as Map;
                    if (data != null)
                    {
                        gmems.Add(data.ToGroupMember(group, m_AvatarNameService));
                    }
                }
                return gmems;
            }
        }

        List<GroupMember> IGroupMembersInterface.this[UUI requestingAgent, UUI principal]
        {
            get 
            {
                Map m = new Map();
                m.Add("AgentID", principal.ID);
                AnArray v = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentGroupMemberships", m) as AnArray;
                if(null == v)
                {
                    throw new AccessFailedException();
                }
                List<GroupMember> gmems = new List<GroupMember>();
                foreach (IValue iv in v)
                {
                    Map data = iv as Map;
                    if(null != data)
                    {
                        gmems.Add(data.ToGroupMember(m_AvatarNameService));
                    }
                }
                return gmems;
            }
        }

        GroupMember IGroupMembersInterface.Add(UUI requestingAgent, UGI group, UUI principal, UUID roleID, string accessToken)
        {
            Map m = new Map();
            m.Add("AgentID", principal.ID);
            m.Add("GroupID", group.ID);
            FlotsamXmlRpcCall(requestingAgent, "groups.addAgentToGroup", m);
            return Members[requestingAgent, group, principal];
        }

        void IGroupMembersInterface.SetContribution(UUI requestingAgent, UGI group, UUI principal, int contribution)
        {
            throw new NotImplementedException();
        }

        void IGroupMembersInterface.Update(UUI requestingAgent, UGI group, UUI principal, bool acceptNotices, bool listInProfile)
        {
            Map m = new Map();
            m.Add("AgentID", principal.ID);
            m.Add("GroupID", group.ID);
            m.Add("AcceptNotices", acceptNotices ? 1 : 0);
            m.Add("ListInProfile", listInProfile ? 1 : 0);
            FlotsamXmlRpcCall(requestingAgent, "groups.setAgentGroupInfo", m);
        }

        void IGroupMembersInterface.Delete(UUI requestingAgent, UGI group, UUI principal)
        {
            Map m = new Map();
            m.Add("AgentID", principal.ID);
            m.Add("GroupID", group.ID);
            FlotsamXmlRpcCall(requestingAgent, "groups.removeAgentFromGroup", m);
        }
    }
}
