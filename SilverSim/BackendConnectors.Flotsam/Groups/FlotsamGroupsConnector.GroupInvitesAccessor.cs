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
        class InvitesAccessor : FlotsamGroupsCommonConnector, IGroupInvitesInterface
        {
            public InvitesAccessor(string uri)
                : base(uri)
            {
            }

            public GroupInvite this[UUI requestingAgent, UUID groupInviteID]
            {
                get 
                {
                    Map m = new Map();
                    m.Add("InviteID", groupInviteID);
                    IValue iv = FlotsamXmlRpcGetCall(requestingAgent, "groups.getAgentToGroupInvite", m);
                    if(!(iv is Map))
                    {
                        throw new AccessFailedException();
                    }
                    m = (Map)iv;

                    GroupInvite inv = new GroupInvite();
                    inv.ID = groupInviteID;
                    inv.Principal.ID = m["AgentID"].AsUUID;
                    inv.Group.ID = m["GroupID"].AsUUID;
                    inv.RoleID = m["RoleID"].AsUUID;
                    return inv;
                }
            }

            public List<GroupInvite> this[UUI requestingAgent, UGI group, UUID roleID, UUI principal]
            {
                get { throw new NotImplementedException(); }
            }

            public List<GroupInvite> this[UUI requestingAgent, UUI principal]
            {
                get { throw new NotImplementedException(); }
            }

            public List<GroupInvite> GetByGroup(UUI requestingAgent, UGI group)
            {
                throw new NotImplementedException();
            }

            public void Add(UUI requestingAgent, GroupInvite invite)
            {
                Map m = new Map();
                m.Add("InviteID", invite.ID);
                m.Add("GroupID", invite.Group.ID);
                m.Add("RoleID", invite.RoleID);
                m.Add("AgentID", invite.Principal.ID);
                FlotsamXmlRpcCall(requestingAgent, "groups.addAgentToGroupInvite", m);
            }

            public void Delete(UUI requestingAgent, UUID inviteID)
            {
                Map m = new Map();
                m.Add("InviteID", inviteID);
                FlotsamXmlRpcCall(requestingAgent, "groups.removeAgentToGroupInvite", m);
            }
        }
    }
}
