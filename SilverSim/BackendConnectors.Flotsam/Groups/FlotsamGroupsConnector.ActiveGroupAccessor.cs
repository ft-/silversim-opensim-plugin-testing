// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector
    {
        public sealed class ActiveGroupAccessor : FlotsamGroupsCommonConnector, IGroupSelectInterface
        {
            public ActiveGroupAccessor(string uri)
                : base(uri)
            {
            }

            public UGI this[UUI requestingAgent, UUI principal]
            {
                get
                {
                    Map m = new Map();
                    m["AgentID"] = principal.ID;
                    m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentActiveMembership", m) as Map;
                    if(null == m)
                    {
                        throw new AccessFailedException();
                    }

                    if(m.ContainsKey("error"))
                    {
                        if(m["error"].ToString() == "No Active Group Specified")
                        {
                            return UGI.Unknown;
                        }
                        throw new AccessFailedException();
                    }

                    UGI res = new UGI();
                    res.ID = m["GroupID"].AsUUID;
                    res.GroupName = m["GroupName"].ToString();
                    return res;
                }
                set
                {
                    Map m = new Map();
                    m["AgentID"] = principal.ID;
                    m["GroupID"] = value.ID;
                    FlotsamXmlRpcCall(requestingAgent, "groups.setAgentActiveGroup", m);
                }
            }

            public UUID this[UUI requestingAgent, UGI group, UUI principal]
            {
                get
                {
                    Map m = new Map();
                    m["AgentID"] = principal.ID;
                    m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentActiveMembership", m) as Map;
                    if (m == null)
                    {
                        throw new AccessFailedException();
                    }

                    if (m.ContainsKey("error"))
                    {
                        if (m["error"].ToString() == "No Active Group Specified")
                        {
                            return UUID.Zero;
                        }
                        throw new AccessFailedException();
                    }

                    return m["SelectedRoleID"].AsUUID;
                }

                set
                {
                    Map m = new Map();
                    m["AgentID"] = principal.ID;
                    m["GroupID"] = group.ID;
                    m["SelectedRoleID"] = value;
                    FlotsamXmlRpcCall(requestingAgent, "groups.setAgentGroupInfo", m);
                }
            }
        }
    }
}
