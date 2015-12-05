// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector
    {
        public sealed class ActiveGroupMembershipAccessor : FlotsamGroupsCommonConnector, IActiveGroupMembershipInterface
        {
            public ActiveGroupMembershipAccessor(string uri)
                : base(uri)
            {
            }

            public bool TryGetValue(UUI requestingAgent, UUI principal, out GroupActiveMembership gam)
            {
                Map m = new Map();
                m["AgentID"] = principal.ID;
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentActiveMembership", m) as Map;
                if (m == null)
                {
                    gam = default(GroupActiveMembership);
                    return false;
                }

                if (m.ContainsKey("error"))
                {
                    if (m["error"].ToString() == "No Active Group Specified")
                    {
                        gam = new GroupActiveMembership();
                        gam.Group = UGI.Unknown;
                        gam.SelectedRoleID = UUID.Zero;
                        gam.User = principal;
                        return true;
                    }
                    gam = default(GroupActiveMembership);
                    return false;
                }

                gam = new GroupActiveMembership();
                gam.Group = UGI.Unknown;
                gam.SelectedRoleID = UUID.Zero;
                gam.User = principal;
                gam.Group.ID = m["GroupID"].AsUUID;
                gam.Group.GroupName = m["GroupName"].ToString();
                gam.SelectedRoleID = m["SelectedRoleID"].AsUUID;
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, UUI principal)
            {
                Map m = new Map();
                m["AgentID"] = principal.ID;
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentActiveMembership", m) as Map;
                if (m == null)
                {
                    return false;
                }

                if (m.ContainsKey("error"))
                {
                    if (m["error"].ToString() == "No Active Group Specified")
                    {
                        return true;
                    }
                    return false;
                }

                return true;
            }

            public GroupActiveMembership this[UUI requestingAgent, UUI principal]
            {
                get 
                {
                    GroupActiveMembership gam;
                    if(!TryGetValue(requestingAgent, principal, out gam))
                    {
                        throw new AccessFailedException();
                    }
                    return gam;
                }
            }
        }
    }
}
