// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector
    {
        class NoticesAccessor : IGroupNoticesInterface
        {
            public int TimeoutMs = 20000;
            string m_Uri;
            GetGroupsAgentIDDelegate m_GetGroupsAgentID;

            public NoticesAccessor(string uri, GetGroupsAgentIDDelegate getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            public List<GroupNotice> GetNotices(UUI requestingAgent, UGI group)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["GroupID"] = (string)group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETNOTICES";
                Map m = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs));
                if (!m.ContainsKey("RESULT"))
                {
                    throw new KeyNotFoundException();
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    throw new KeyNotFoundException();
                }

                List<GroupNotice> groupnotices = new List<GroupNotice>();
                foreach (IValue iv in ((Map)m["RESULT"]).Values)
                {
                    if (iv is Map)
                    {
                        GroupNotice notice = new GroupNotice();
                        notice.Group = group;
                        groupnotices.Add(notice);
                    }
                }

                return groupnotices;
            }

            public GroupNotice this[UUI requestingAgent, UUID groupNoticeID]
            {
                get 
                {
                    Dictionary<string, string> post = new Dictionary<string, string>();
                    post["InviteID"] = (string)groupNoticeID;
                    post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                    post["OP"] = "GET";
                    post["METHOD"] = "INVITE";
                    Map m = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs));
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

            public void Add(UUI requestingAgent, GroupNotice notice)
            {
                Dictionary<string, string> post = notice.ToPost(m_GetGroupsAgentID);
                post["GroupID"] = (string)notice.Group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "ADDNOTICE";
                BooleanResponseRequest(m_Uri, post, false, TimeoutMs);
            }

            public void Delete(UUI requestingAgent, UUID groupNoticeID)
            {
                throw new NotImplementedException();
            }
        }
    }
}
