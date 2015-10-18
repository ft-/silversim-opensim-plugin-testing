// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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
            string m_Uri;
            Func<UUI, string> m_GetGroupsAgentID;

            public ActiveGroupAccessor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public UGI this[UUI requestingAgent, UUI principal]
            {
                get
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETMEMBERSHIP";

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

                    return m["RESULT"].ToGroupMemberFromMembership().Group;
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

            public UUID this[UUI requestingAgent, UGI group, UUI principal]
            {
                get
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETMEMBERSHIP";

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

                    return m["RESULT"].ToGroupMemberFromMembership().SelectedRoleID;
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
