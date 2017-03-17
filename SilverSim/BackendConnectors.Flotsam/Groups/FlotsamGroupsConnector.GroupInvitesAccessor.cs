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

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector : GroupsServiceInterface.IGroupInvitesInterface
    {
        bool IGroupInvitesInterface.ContainsKey(UUI requestingAgent, UUID groupInviteID)
        {
            GroupInvite inv;
            return Invites.TryGetValue(requestingAgent, groupInviteID, out inv);
        }

        bool IGroupInvitesInterface.TryGetValue(UUI requestingAgent, UUID groupInviteID, out GroupInvite inv)
        {
            Map m = new Map();
            m.Add("InviteID", groupInviteID);
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentToGroupInvite", m) as Map;
            if (null == m)
            {
                inv = default(GroupInvite);
                return false;
            }

            inv = new GroupInvite();
            inv.ID = groupInviteID;
            inv.Principal.ID = m["AgentID"].AsUUID;
            inv.Group.ID = m["GroupID"].AsUUID;
            inv.RoleID = m["RoleID"].AsUUID;
            inv.Principal = m_AvatarNameService.ResolveName(inv.Principal);
            return true;
        }

        GroupInvite IGroupInvitesInterface.this[UUI requestingAgent, UUID groupInviteID]
        {
            get 
            {
                GroupInvite inv;
                if (!Invites.TryGetValue(requestingAgent, groupInviteID, out inv))
                {
                    throw new KeyNotFoundException();
                }
                return inv;
            }
        }

        bool IGroupInvitesInterface.DoesSupportListGetters
        {
            get
            {
                return false;
            }
        }

        List<GroupInvite> IGroupInvitesInterface.this[UUI requestingAgent, UGI group, UUID roleID, UUI principal]
        {
            get { throw new NotSupportedException(); }
        }

        List<GroupInvite> IGroupInvitesInterface.this[UUI requestingAgent, UUI principal]
        {
            get { throw new NotSupportedException(); }
        }

        List<GroupInvite> IGroupInvitesInterface.GetByGroup(UUI requestingAgent, UGI group)
        {
            throw new NotSupportedException();
        }

        void IGroupInvitesInterface.Add(UUI requestingAgent, GroupInvite invite)
        {
            Map m = new Map();
            m.Add("InviteID", invite.ID);
            m.Add("GroupID", invite.Group.ID);
            m.Add("RoleID", invite.RoleID);
            m.Add("AgentID", invite.Principal.ID);
            FlotsamXmlRpcCall(requestingAgent, "groups.addAgentToGroupInvite", m);
        }

        void IGroupInvitesInterface.Delete(UUI requestingAgent, UUID inviteID)
        {
            Map m = new Map();
            m.Add("InviteID", inviteID);
            FlotsamXmlRpcCall(requestingAgent, "groups.removeAgentToGroupInvite", m);
        }
    }
}
