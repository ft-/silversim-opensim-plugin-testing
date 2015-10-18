// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        public sealed class MembershipsAccessor : IGroupMembershipsInterface
        {
            public int TimeoutMs = 20000;
            string m_Uri;
            Func<UUI, string> m_GetGroupsAgentID;

            public MembershipsAccessor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public GroupMembership this[UUI requestingAgent, UGI group, UUI principal]
            {
                get
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["GroupID"] = (string)group.ID;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETMEMBERSHIP";
                    Map m;
                    using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                    {
                        m = OpenSimResponse.Deserialize(s);
                    }
                    if (!m.ContainsKey("RESULT"))
                    {
                        throw new AccessFailedException();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        throw new AccessFailedException(m["REASON"].ToString());
                    }

                    return m["RESULT"].ToGroupMembership();
                }
            }

            public List<GroupMembership> this[UUI requestingAgent, UUI principal]
            {
                get 
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["ALL"] = "true";
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETMEMBERSHIP";
                    Map m;
                    using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                    {
                        m = OpenSimResponse.Deserialize(s);
                    }
                    if (!m.ContainsKey("RESULT"))
                    {
                        throw new AccessFailedException();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        if(m["REASON"].ToString() == "No memberships")
                        {
                            return new List<GroupMembership>();
                        }
                        throw new AccessFailedException(m["REASON"].ToString());
                    }

                    Map resultmap = m["RESULT"] as Map;
                    if(null == resultmap)
                    {
                        return new List<GroupMembership>();
                    }

                    List<GroupMembership> members = new List<GroupMembership>();
                    foreach (IValue iv in resultmap.Values)
                    {
                        GroupMembership member = iv.ToGroupMembership();
                        member.Principal = principal;
                        members.Add(member);
                    }
                    return members;
                }
            }
        }
    }
}
