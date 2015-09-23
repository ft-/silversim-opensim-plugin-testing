// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector
    {
        class MembersAccessor : FlotsamGroupsCommonConnector, IGroupMembersInterface
        {
            public MembersAccessor(string uri)
                : base(uri)
            {
            }

            public GroupMember this[UUI requestingAgent, UGI group, UUI principal]
            {
                get 
                {
                    IEnumerable<GroupMember> e = this[requestingAgent, group].Where(p => p.Principal.ID == principal.ID);
                    foreach(GroupMember g in e)
                    {
                        return g;
                    }
                    throw new KeyNotFoundException();
                }
            }

            public List<GroupMember> this[UUI requestingAgent, UGI group]
            {
                get 
                {
                    Map m = new Map();
                    m.Add("GroupID", group.ID);
                    IValue v = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupMembers", m);
                    if(!(v is AnArray))
                    {
                        throw new AccessFailedException();
                    }
                    List<GroupMember> gmems = new List<GroupMember>();
                    foreach(IValue iv in (AnArray)v)
                    {
                        if(iv is Map)
                        {
                            gmems.Add(iv.ToGroupMember(group));
                        }
                    }
                    return gmems;
                }
            }

            public List<GroupMember> this[UUI requestingAgent, UUI principal]
            {
                get 
                {
                    Map m = new Map();
                    m.Add("AgentID", principal.ID);
                    IValue v = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentGroupMemberships", m);
                    if (!(v is AnArray))
                    {
                        throw new AccessFailedException();
                    }
                    List<GroupMember> gmems = new List<GroupMember>();
                    foreach (IValue iv in (AnArray)v)
                    {
                        if (iv is Map)
                        {
                            gmems.Add(iv.ToGroupMember());
                        }
                    }
                    return gmems;
                }
            }

            public GroupMember Add(UUI requestingAgent, UGI group, UUI principal, UUID roleID, string accessToken)
            {
                Map m = new Map();
                m.Add("AgentID", principal.ID);
                m.Add("GroupID", group.ID);
                FlotsamXmlRpcCall(requestingAgent, "groups.addAgentToGroup", m);
                return this[requestingAgent, group, principal];
            }

            public void SetContribution(UUI requestingAgent, UGI group, UUI principal, int contribution)
            {
                throw new NotImplementedException();
            }

            public void Update(UUI requestingAgent, UGI group, UUI principal, bool acceptNotices, bool listInProfile)
            {
                Map m = new Map();
                m.Add("AgentID", principal.ID);
                m.Add("GroupID", group.ID);
                m.Add("AcceptNotices", acceptNotices ? 1 : 0);
                m.Add("ListInProfile", listInProfile ? 1 : 0);
                FlotsamXmlRpcCall(requestingAgent, "groups.setAgentGroupInfo", m);
            }

            public void Delete(UUI requestingAgent, UGI group, UUI principal)
            {
                Map m = new Map();
                m.Add("AgentID", principal.ID);
                m.Add("GroupID", group.ID);
                FlotsamXmlRpcCall(requestingAgent, "groups.removeAgentFromGroup", m);
            }
        }
    }
}
