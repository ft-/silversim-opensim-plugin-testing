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
        bool IGroupMembersInterface.TryGetValue(UGUI requestingAgent, UGI group, UGUI principal, out GroupMember gmem)
        {
            foreach (GroupMember g in Members[requestingAgent, group].Where(p => p.Principal.ID == principal.ID))
            {
                gmem = g;
                return true;
            }
            gmem = default(GroupMember);
            return false;
        }

        bool IGroupMembersInterface.ContainsKey(UGUI requestingAgent, UGI group, UGUI principal)
        {
            foreach (GroupMember g in Members[requestingAgent, group].Where(p => p.Principal.ID == principal.ID))
            {
                return true;
            }
            return false;
        }

        GroupMember IGroupMembersInterface.this[UGUI requestingAgent, UGI group, UGUI principal]
        {
            get
            {
                foreach(GroupMember g in Members[requestingAgent, group].Where(p => p.Principal.ID == principal.ID))
                {
                    return g;
                }
                throw new KeyNotFoundException();
            }
        }

        List<GroupMember> IGroupMembersInterface.this[UGUI requestingAgent, UGI group]
        {
            get
            {
                var m = new Map
                {
                    ["GroupID"] = group.ID
                };
                var res = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupMembers", m) as AnArray;
                if(res == null)
                {
                    throw new AccessFailedException();
                }
                var gmems = new List<GroupMember>();
                foreach (IValue iv in res)
                {
                    var data = iv as Map;
                    if (data != null)
                    {
                        gmems.Add(data.ToGroupMember(group, m_AvatarNameService));
                    }
                }
                return gmems;
            }
        }

        List<GroupMember> IGroupMembersInterface.this[UGUI requestingAgent, UGUI principal]
        {
            get
            {
                var m = new Map
                {
                    ["AgentID"] = principal.ID
                };
                var v = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentGroupMemberships", m) as AnArray;
                if(v == null)
                {
                    throw new AccessFailedException();
                }
                var gmems = new List<GroupMember>();
                foreach (IValue iv in v)
                {
                    var data = iv as Map;
                    if(data != null)
                    {
                        gmems.Add(data.ToGroupMember(m_AvatarNameService));
                    }
                }
                return gmems;
            }
        }

        GroupMember IGroupMembersInterface.Add(UGUI requestingAgent, UGI group, UGUI principal, UUID roleID, string accessToken)
        {
            var m = new Map
            {
                ["AgentID"] = principal.ID,
                ["GroupID"] = group.ID
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.addAgentToGroup", m);
            return Members[requestingAgent, group, principal];
        }

        void IGroupMembersInterface.SetContribution(UGUI requestingAgent, UGI group, UGUI principal, int contribution)
        {
            throw new NotImplementedException();
        }

        void IGroupMembersInterface.Update(UGUI requestingAgent, UGI group, UGUI principal, bool acceptNotices, bool listInProfile)
        {
            var m = new Map
            {
                { "AgentID", principal.ID },
                { "GroupID", group.ID },
                { "AcceptNotices", acceptNotices ? 1 : 0 },
                { "ListInProfile", listInProfile ? 1 : 0 }
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.setAgentGroupInfo", m);
        }

        void IGroupMembersInterface.Delete(UGUI requestingAgent, UGI group, UGUI principal)
        {
            var m = new Map
            {
                ["AgentID"] = principal.ID,
                ["GroupID"] = group.ID
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.removeAgentFromGroup", m);
        }
    }
}
