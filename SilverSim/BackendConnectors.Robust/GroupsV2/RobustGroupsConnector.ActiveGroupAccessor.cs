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
using System;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        public sealed class ActiveGroupAccessor : IGroupSelectInterface
        {
            public int TimeoutMs = 20000;
            private readonly string m_Uri;
            private readonly Func<UUI, string> m_GetGroupsAgentID;

            public ActiveGroupAccessor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public bool TryGetValue(UUI requestingAgent, UUI principal, out UGI ugi)
            {
                var post = new Dictionary<string, string>
                {
                    ["AgentID"] = m_GetGroupsAgentID(principal),
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETMEMBERSHIP"
                };
                Map m;
                using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    ugi = default(UGI);
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    ugi = default(UGI);
                    return false;
                }

                ugi = m["RESULT"].ToGroupMemberFromMembership().Group;
                return true;
            }

            public UGI this[UUI requestingAgent, UUI principal]
            {
                get
                {
                    UGI ugi;
                    if(!TryGetValue(requestingAgent, principal, out ugi))
                    {
                        throw new KeyNotFoundException();
                    }
                    return ugi;
                }
                set
                {
                    var post = new Dictionary<string, string>
                    {
                        ["AgentID"] = m_GetGroupsAgentID(principal),
                        ["GroupID"] = (string)value.ID,
                        ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                        ["OP"] = "GROUP",
                        ["METHOD"] = "SETACTIVE"
                    };
                    Map m;
                    using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                    {
                        m = OpenSimResponse.Deserialize(s);
                    }
                    if (!m.ContainsKey("RESULT"))
                    {
                        throw new KeyNotFoundException();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        throw new KeyNotFoundException();
                    }
                }
            }

            public bool TryGetValue(UUI requestingAgent, UGI group, UUI principal, out UUID id)
            {
                var post = new Dictionary<string, string>
                {
                    ["AgentID"] = m_GetGroupsAgentID(principal),
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETMEMBERSHIP"
                };
                Map m;
                using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    id = default(UUID);
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    id = default(UUID);
                    return false;
                }

                id = m["RESULT"].ToGroupMemberFromMembership().SelectedRoleID;
                return true;
            }

            public UUID this[UUI requestingAgent, UGI group, UUI principal]
            {
                get
                {
                    UUID id;
                    if(!TryGetValue(requestingAgent, group, principal, out id))
                    {
                        throw new KeyNotFoundException();
                    }
                    return id;
                }
                set
                {
                    var post = new Dictionary<string, string>
                    {
                        ["AgentID"] = m_GetGroupsAgentID(principal),
                        ["GroupID"] = (string)group.ID,
                        ["RoleID"] = (string)value,
                        ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                        ["OP"] = "ROLE",
                        ["METHOD"] = "SETACTIVE"
                    };
                    Map m;
                    using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                    {
                        m = OpenSimResponse.Deserialize(s);
                    }
                    if (!m.ContainsKey("RESULT"))
                    {
                        throw new KeyNotFoundException();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        throw new KeyNotFoundException();
                    }
                }
            }
        }
    }
}
