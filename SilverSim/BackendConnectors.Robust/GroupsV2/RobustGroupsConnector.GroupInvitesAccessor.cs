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

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
        public sealed class InvitesAccessor : IGroupInvitesInterface
        {
            public int TimeoutMs = 20000;
            readonly string m_Uri;
            readonly Func<UUI, string> m_GetGroupsAgentID;

            public InvitesAccessor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public bool ContainsKey(UUI requestingAgent, UUID groupInviteID)
            {
                GroupInvite inv;
                return TryGetValue(requestingAgent, groupInviteID, out inv);
            }

            public bool TryGetValue(UUI requestingAgent, UUID groupInviteID, out GroupInvite gi)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["InviteID"] = (string)groupInviteID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["OP"] = "GET";
                post["METHOD"] = "INVITE";

                Map m;
                using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    gi = default(GroupInvite);
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    gi = default(GroupInvite);
                    return false;
                }

                Map resultMap = m["RESULT"] as Map;
                if (null == resultMap)
                {
                    gi = default(GroupInvite);
                    return false;
                }
                gi = new GroupInvite();
                gi.ID = resultMap["InviteID"].AsUUID;
                gi.Group.ID = resultMap["GroupID"].AsUUID;
                gi.RoleID = resultMap["RoleID"].AsUUID;
                gi.Principal.ID = resultMap["AgentID"].AsUUID;

                return true;
            }

            public GroupInvite this[UUI requestingAgent, UUID groupInviteID]
            {
                get 
                {
                    GroupInvite gi;
                    if(!TryGetValue(requestingAgent, groupInviteID, out gi))
                    {
                        throw new KeyNotFoundException();
                    }
                    return gi;
                }
            }

            bool IGroupInvitesInterface.DoesSupportListGetters
            {
                get
                {
                    return false;
                }
            }

            public List<GroupInvite> this[UUI requestingAgent, UGI group, UUID roleID, UUI principal]
            {
                get 
                {
                    throw new NotSupportedException();
                }
            }

            public List<GroupInvite> this[UUI requestingAgent, UUI principal]
            {
                get
                {
                    throw new NotSupportedException();
                }
            }

            public List<GroupInvite> GetByGroup(UUI requestingAgent, UGI group)
            {
                throw new NotSupportedException();
            }

            public void Add(UUI requestingAgent, GroupInvite invite)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["InviteID"] = (string)invite.ID;
                post["GroupID"] = (string)invite.Group.ID;
                post["RoleID"] = (string)invite.RoleID;
                post["AgentID"] = m_GetGroupsAgentID(invite.Principal);
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["OP"] = "ADD";
                post["METHOD"] = "INVITE";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }

            public void Delete(UUI requestingAgent, UUID inviteID)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["METHOD"] = "INVITE";
                post["OP"] = "DELETE";
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["InviteID"] = (string)inviteID;
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }
        }
    }
}
