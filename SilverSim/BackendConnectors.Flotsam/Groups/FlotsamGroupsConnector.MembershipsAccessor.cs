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
        public sealed class MembershipsAccessor : FlotsamGroupsCommonConnector, IGroupMembershipsInterface
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
                    m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentGroupMembership", m) as Map;
                    if (null == m)
                    {
                        throw new AccessFailedException();
                    }
                    return m.ToGroupMembership();
                }
            }

            public List<GroupMembership> this[UUI requestingAgent, UUI principal]
            {
                get 
                {
                    Map m = new Map();
                    m.Add("AgentID", principal.ID);
                    AnArray res = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentGroupMemberships", m) as AnArray;
                    if (null == res)
                    {
                        throw new AccessFailedException();
                    }
                    List<GroupMembership> gmems = new List<GroupMembership>();
                    foreach (IValue iv in res)
                    {
                        Map data = iv as Map;
                        if (null != data)
                        {
                            gmems.Add(data.ToGroupMembership());
                        }
                    }
                    return gmems;
                }
            }
        }
    }
}
