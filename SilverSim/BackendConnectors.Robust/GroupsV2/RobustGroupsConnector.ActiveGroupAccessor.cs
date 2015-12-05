// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
        public sealed class ActiveGroupAccessor : IGroupSelectInterface
        {
            public int TimeoutMs = 20000;
            readonly string m_Uri;
            readonly Func<UUI, string> m_GetGroupsAgentID;

            public ActiveGroupAccessor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public bool TryGetValue(UUI requestingAgent, UUI principal, out UGI ugi)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETMEMBERSHIP";

                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["GroupID"] = (string)value.ID;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["OP"] = "GROUP";
                    post["METHOD"] = "SETACTIVE";

                    Map m;
                    using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETMEMBERSHIP";

                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["GroupID"] = (string)group.ID;
                    post["RoleID"] = (string)value;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["OP"] = "ROLE";
                    post["METHOD"] = "SETACTIVE";

                    Map m;
                    using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
