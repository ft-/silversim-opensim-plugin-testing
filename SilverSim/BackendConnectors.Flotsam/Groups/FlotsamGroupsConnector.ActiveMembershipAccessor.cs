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
using System.Text;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector : GroupsServiceInterface.IActiveGroupMembershipInterface
    {
        bool IActiveGroupMembershipInterface.TryGetValue(UUI requestingAgent, UUI principal, out GroupActiveMembership gam)
        {
            var m = new Map
            {
                ["AgentID"] = principal.ID
            };
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
                    gam = new GroupActiveMembership()
                    {
                        Group = UGI.Unknown,
                        SelectedRoleID = UUID.Zero,
                        User = principal
                    };
                    return true;
                }
                gam = default(GroupActiveMembership);
                return false;
            }

            gam = new GroupActiveMembership()
            {
                User = m_AvatarNameService.ResolveName(principal),
                Group = new UGI { ID = m["GroupID"].AsUUID, GroupName = m["GroupName"].ToString() },
                SelectedRoleID = m["SelectedRoleID"].AsUUID
            };
            return true;
        }

        bool IActiveGroupMembershipInterface.ContainsKey(UUI requestingAgent, UUI principal)
        {
            var m = new Map
            {
                ["AgentID"] = principal.ID
            };
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

        GroupActiveMembership IActiveGroupMembershipInterface.this[UUI requestingAgent, UUI principal]
        {
            get
            {
                GroupActiveMembership gam;
                if(!ActiveMembership.TryGetValue(requestingAgent, principal, out gam))
                {
                    throw new AccessFailedException();
                }
                return gam;
            }
        }
    }
}
