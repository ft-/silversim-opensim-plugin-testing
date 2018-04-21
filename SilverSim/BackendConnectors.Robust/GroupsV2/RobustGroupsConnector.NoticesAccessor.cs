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
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        public sealed class NoticesAccessor : IGroupNoticesInterface
        {
            public int TimeoutMs = 20000;
            private readonly string m_Uri;
            private readonly Func<UGUI, string> m_GetGroupsAgentID;

            public NoticesAccessor(string uri, Func<UGUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public List<GroupNotice> GetNotices(UGUI requestingAgent, UGI group)
            {
                var post = new Dictionary<string, string>
                {
                    ["GroupID"] = (string)group.ID,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETNOTICES"
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

                var resultmap = m["RESULT"] as Map;

                var groupnotices = new List<GroupNotice>();
                foreach (IValue iv in resultmap.Values)
                {
                    if (iv is Map)
                    {
                        var notice = new GroupNotice
                        {
                            Group = group
                        };
                        groupnotices.Add(notice);
                    }
                }

                return groupnotices;
            }

            public bool TryGetValue(UGUI requestingAgent, UUID groupNoticeID, out GroupNotice notice)
            {
                var post = new Dictionary<string, string>
                {
                    ["InviteID"] = (string)groupNoticeID,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["OP"] = "GET",
                    ["METHOD"] = "INVITE"
                };
                Map m;
                using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    notice = default(GroupNotice);
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    notice = default(GroupNotice);
                    return false;
                }

                notice = m["RESULT"].ToGroupNotice();
#warning TODO: GroupNotice service does not deliver any group ID in response
                return true;
            }

            public bool ContainsKey(UGUI requestingAgent, UUID groupNoticeID)
            {
                var post = new Dictionary<string, string>
                {
                    ["InviteID"] = (string)groupNoticeID,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["OP"] = "GET",
                    ["METHOD"] = "INVITE"
                };
                Map m;
                using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
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

            public GroupNotice this[UGUI requestingAgent, UUID groupNoticeID]
            {
                get
                {
                    var post = new Dictionary<string, string>
                    {
                        ["InviteID"] = (string)groupNoticeID,
                        ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                        ["OP"] = "GET",
                        ["METHOD"] = "INVITE"
                    };
                    Map m;
                    using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
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

                    GroupNotice notice = m["RESULT"].ToGroupNotice();
#warning TODO: GroupNotice service does not deliver any group ID in response
                    return notice;
                }
            }

            public void Add(UGUI requestingAgent, GroupNotice notice)
            {
                Dictionary<string, string> post = notice.ToPost(m_GetGroupsAgentID);
                post["GroupID"] = (string)notice.Group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "ADDNOTICE";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }

            public void Delete(UGUI requestingAgent, UUID groupNoticeID)
            {
                throw new NotSupportedException();
            }
        }
    }
}
