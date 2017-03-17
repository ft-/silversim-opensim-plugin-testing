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
        public sealed class MembersAccessor : IGroupMembersInterface
        {
            public int TimeoutMs = 20000;
            readonly string m_Uri;
            readonly Func<UUI, string> m_GetGroupsAgentID;

            public MembersAccessor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public bool TryGetValue(UUI requestingAgent, UGI group, UUI principal, out GroupMember member)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["GroupID"] = (string)group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETMEMBERSHIP";
                Map m;
                using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    member = default(GroupMember);
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    member = default(GroupMember);
                    return false;
                }

                member = m["RESULT"].ToGroupMemberFromMembership();
                member.Principal = principal;
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, UGI group, UUI principal)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["GroupID"] = (string)group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETMEMBERSHIP";
                Map m;
                using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    return false;
                }

                return true;
            }

            public GroupMember this[UUI requestingAgent, UGI group, UUI principal]
            {
                get 
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["GroupID"] = (string)group.ID;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETMEMBERSHIP";
                    Map m;
                    using(Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
                    {
                        m = OpenSimResponse.Deserialize(s);
                    }
                    if (!m.ContainsKey("RESULT"))
                    {
                        throw new AccessFailedException();
                    }
                    if (m["RESULT"].ToString() == "NULL")
                    {
                        throw new KeyNotFoundException(m["REASON"].ToString());
                    }

                    GroupMember member = m["RESULT"].ToGroupMemberFromMembership();
                    member.Principal = principal;
                    return member;
                }
            }

            public List<GroupMember> this[UUI requestingAgent, UGI group]
            {
                get
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["GroupID"] = (string)group.ID;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETGROUPMEMBERS";
                    Map m;
                    using(Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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

                    List<GroupMember> members = new List<GroupMember>();
                    foreach (IValue iv in ((Map)m["RESULT"]).Values)
                    {
                        GroupMember member = iv.ToGroupMember(group);
                        member.Principal = requestingAgent;
                        members.Add(member);
                    }
                    return members;
                }
            }

            public List<GroupMember> this[UUI requestingAgent, UUI principal]
            {
                get 
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["AgentID"] = m_GetGroupsAgentID(principal);
                    post["ALL"] = "true";
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["METHOD"] = "GETMEMBERSHIP";
                    Map m;
                    using(Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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

                    List<GroupMember> members = new List<GroupMember>();
                    foreach (IValue iv in ((Map)m["RESULT"]).Values)
                    {
                        GroupMember member = iv.ToGroupMemberFromMembership();
                        member.Principal = principal;
                        members.Add(member);
                    }
                    return members;
                }
            }

            public GroupMember Add(UUI requestingAgent, UGI group, UUI principal, UUID roleID, string accessToken)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["GroupID"] = (string)group.ID;
                post["RoleID"] = (string)roleID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["AccessToken"] = accessToken;
                post["METHOD"] = "ADDAGENTTOGROUP";
                Map m;
                using(Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
                if(!(m["RESULT"] is Map))
                {
                    throw new AccessFailedException();
                }
                GroupMember member = m["RESULT"].ToGroupMemberFromMembership();
                member.Principal = principal;
                return member;
            }

            public void SetContribution(UUI requestingAgent, UGI group, UUI principal, int contribution)
            {
                throw new NotSupportedException();
            }

            public void Update(UUI requestingAgent, UGI group, UUI principal, bool acceptNotices, bool listInProfile)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["GroupID"] = (string)group.ID;
                post["AcceptNotices"] = acceptNotices.ToString();
                post["ListInProfile"] = listInProfile.ToString();
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "UPDATEMEMBERSHIP";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }

            public void Delete(UUI requestingAgent, UGI group, UUI principal)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["AgentID"] = m_GetGroupsAgentID(principal);
                post["GroupID"] = (string)group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "REMOVEAGENTFROMGROUP";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }
        }
    }
}
