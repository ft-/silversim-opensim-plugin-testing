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

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector : GroupsServiceInterface.IGroupSelectInterface
    {
        bool IGroupSelectInterface.TryGetValue(UGUI requestingAgent, UGUI principal, out UGI ugi)
        {
            var m = new Map
            {
                ["AgentID"] = principal.ID
            };
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentActiveMembership", m) as Map;
            if (m == null)
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

            ugi = new UGI
            {
                ID = m["GroupID"].AsUUID,
                GroupName = m["GroupName"].ToString()
            };
            return true;
        }

        UGI IGroupSelectInterface.this[UGUI requestingAgent, UGUI principal]
        {
            get
            {
                UGI ugi;
                if(!ActiveGroup.TryGetValue(requestingAgent, principal, out ugi))
                {
                    throw new AccessFailedException();
                }
                return ugi;
            }
            set
            {
                var m = new Map
                {
                    ["AgentID"] = principal.ID,
                    ["GroupID"] = value.ID
                };
                FlotsamXmlRpcCall(requestingAgent, "groups.setAgentActiveGroup", m);
            }
        }

        bool IGroupSelectInterface.TryGetValue(UGUI requestingAgent, UGI group, UGUI principal, out UUID id)
        {
            var m = new Map
            {
                ["AgentID"] = principal.ID
            };
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

        UUID IGroupSelectInterface.this[UGUI requestingAgent, UGI group, UGUI principal]
        {
            get
            {
                UUID id;
                if(!ActiveGroup.TryGetValue(requestingAgent, group, principal, out id))
                {
                    throw new AccessFailedException();
                }
                return id;
            }

            set
            {
                var m = new Map
                {
                    ["AgentID"] = principal.ID,
                    ["GroupID"] = group.ID,
                    ["SelectedRoleID"] = value
                };
                FlotsamXmlRpcCall(requestingAgent, "groups.setAgentGroupInfo", m);
            }
        }
    }
}
