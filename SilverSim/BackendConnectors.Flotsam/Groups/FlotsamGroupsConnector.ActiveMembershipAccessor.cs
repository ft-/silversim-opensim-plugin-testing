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

            public GroupActiveMembership this[UUI requestingAgent, UUI principal]
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

                    GroupActiveMembership gam = new GroupActiveMembership();
                    gam.Group = UGI.Unknown;
                    gam.SelectedRoleID = UUID.Zero;
                    gam.User = principal;
                    if (m.ContainsKey("error"))
                    {
                        if (m["error"].ToString() == "No Active Group Specified")
                        {
                            return gam;
                        }
                        throw new AccessFailedException();
                    }

                    gam.Group.ID = m["GroupID"].AsUUID;
                    gam.Group.GroupName = m["GroupName"].ToString();
                    gam.SelectedRoleID = m["SelectedRoleID"].AsUUID;
                    return gam;
                }
            }
        }
    }
}
