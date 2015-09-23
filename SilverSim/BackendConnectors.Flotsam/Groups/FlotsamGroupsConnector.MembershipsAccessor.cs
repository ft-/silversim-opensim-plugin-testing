// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector
    {
        class MembershipsAccessor : FlotsamGroupsCommonConnector, IGroupMembershipsInterface
        {
            public MembershipsAccessor(string uri)
                : base(uri)
            {

            }

            public GroupMembership this[UUI requestingAgent, UGI group, UUI principal]
            {
                get
                {
                    Map m = new Map();
                    m.Add("AgentID", principal.ID);
                    m.Add("GroupID", group.ID);
                    IValue v = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentGroupMembership", m);
                    if (!(v is Map))
                    {
                        throw new AccessFailedException();
                    }
                    return v.ToGroupMembership();
                }
            }

            public List<GroupMembership> this[UUI requestingAgent, UUI principal]
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
                    List<GroupMembership> gmems = new List<GroupMembership>();
                    foreach (IValue iv in (AnArray)v)
                    {
                        if (iv is Map)
                        {
                            gmems.Add(iv.ToGroupMembership());
                        }
                    }
                    return gmems;
                }
            }
        }
    }
}
