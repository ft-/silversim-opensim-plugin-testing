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

            public bool TryGetValue(UUI requestingAgent, UUI principal, out UGI ugi)
            {
                Map m = new Map();
                m["AgentID"] = principal.ID;
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentActiveMembership", m) as Map;
                if (null == m)
                {
                    ugi = default(UGI);
                    return false;
                }

                if (m.ContainsKey("error"))
                {
                    if (m["error"].ToString() == "No Active Group Specified")
                    {
                        ugi = UGI.Unknown;
                        return true;
                    }
                    ugi = default(UGI);
                    return false;
                }

                ugi = new UGI();
                ugi.ID = m["GroupID"].AsUUID;
                ugi.GroupName = m["GroupName"].ToString();
                return true;
            }

            public UGI this[UUI requestingAgent, UUI principal]
            {
                get
                {
                    UGI ugi;
                    if(!TryGetValue(requestingAgent, principal, out ugi))
                    {
                        throw new AccessFailedException();
                    }
                    return ugi;
                }
                set
                {
                    Map m = new Map();
                    m["AgentID"] = principal.ID;
                    m["GroupID"] = value.ID;
                    FlotsamXmlRpcCall(requestingAgent, "groups.setAgentActiveGroup", m);
                }
            }

            public bool TryGetValue(UUI requestingAgent, UGI group, UUI principal, out UUID id)
            {
                Map m = new Map();
                m["AgentID"] = principal.ID;
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentActiveMembership", m) as Map;
                if (m == null)
                {
                    id = default(UUID);
                    return false;
                }

                if (m.ContainsKey("error"))
                {
                    if (m["error"].ToString() == "No Active Group Specified")
                    {
                        id = UUID.Zero;
                        return true;
                    }
                    id = default(UUID);
                    return false;
                }

                id = m["SelectedRoleID"].AsUUID;
                return true;
            }

            public UUID this[UUI requestingAgent, UGI group, UUI principal]
            {
                get
                {
                    UUID id;
                    if(!TryGetValue(requestingAgent, group, principal, out id))
                    {
                        throw new AccessFailedException();
                    }
                    return id;
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
