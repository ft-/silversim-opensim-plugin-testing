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
        public sealed class GroupsAccessor : IGroupsInterface
        {
            public int TimeoutMs = 20000;
            private readonly string m_Uri;
            private readonly Func<UUI, string> m_GetGroupsAgentID;

            public GroupsAccessor(string uri, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            private GroupInfo CreateOrUpdate(UUI requestingAgent, GroupInfo group, string op)
            {
                Dictionary<string, string> post = group.ToPost();
                post.Remove("OwnerRoleID");
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["OP"] = op;
                post["METHOD"] = "PUTGROUP";
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

                return m["RESULT"].ToGroup();
            }

            public GroupInfo Create(UUI requestingAgent, GroupInfo group) =>
                CreateOrUpdate(requestingAgent, group, "ADD");

            public GroupInfo Update(UUI requestingAgent, GroupInfo group) =>
                CreateOrUpdate(requestingAgent, group, "UPDATE");

            public void Delete(UUI requestingAgent, UGI group)
            {
                throw new NotSupportedException();
            }

            public bool TryGetValue(UUI requestingAgent, UUID groupID, out UGI ugi)
            {
                var post = new Dictionary<string, string>
                {
                    ["GroupID"] = (string)groupID,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETGROUP"
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

                ugi = m["RESULT"].ToGroup().ID;
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, UUID groupID)
            {
                var post = new Dictionary<string, string>
                {
                    ["GroupID"] = (string)groupID,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETGROUP"
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

            public UGI this[UUI requestingAgent, UUID groupID]
            {
                get
                {
                    UGI ugi;
                    if(!TryGetValue(requestingAgent, groupID, out ugi))
                    {
                        throw new KeyNotFoundException();
                    }
                    return ugi;
                }
            }

            public bool TryGetValue(UUI requestingAgent, UGI group, out GroupInfo groupInfo)
            {
                var post = new Dictionary<string, string>
                {
                    ["GroupID"] = (string)group.ID,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETGROUP"
                };
                Map m;
                using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    groupInfo = default(GroupInfo);
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    groupInfo = default(GroupInfo);
                    return false;
                }

                groupInfo = m["RESULT"].ToGroup();
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, UGI group)
            {
                var post = new Dictionary<string, string>
                {
                    ["GroupID"] = (string)group.ID,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETGROUP"
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

            public GroupInfo this[UUI requestingAgent, UGI group]
            {
                get
                {
                    GroupInfo groupInfo;
                    if(!TryGetValue(requestingAgent, group, out groupInfo))
                    {
                        throw new KeyNotFoundException();
                    }
                    return groupInfo;
                }
            }

            public bool TryGetValue(UUI requestingAgent, string groupName, out GroupInfo groupInfo)
            {
                var post = new Dictionary<string, string>
                {
                    ["Name"] = groupName,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETGROUP"
                };
                Map m;
                using (Stream s = new HttpClient.Post(m_Uri, post) { TimeoutMs = TimeoutMs }.ExecuteStreamRequest())
                {
                    m = OpenSimResponse.Deserialize(s);
                }
                if (!m.ContainsKey("RESULT"))
                {
                    groupInfo = default(GroupInfo);
                    return false;
                }
                if (m["RESULT"].ToString() == "NULL")
                {
                    groupInfo = default(GroupInfo);
                    return false;
                }

                groupInfo = m["RESULT"].ToGroup();
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, string groupName)
            {
                var post = new Dictionary<string, string>
                {
                    ["Name"] = groupName,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "GETGROUP"
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

            public GroupInfo this[UUI requestingAgent, string groupName]
            {
                get
                {
                    GroupInfo groupInfo;
                    if(!TryGetValue(requestingAgent, groupName, out groupInfo))
                    {
                        throw new KeyNotFoundException();
                    }
                    return groupInfo;
                }
            }

            public List<DirGroupInfo> GetGroupsByName(UUI requestingAgent, string query)
            {
                var post = new Dictionary<string, string>
                {
                    ["Query"] = query,
                    ["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent),
                    ["METHOD"] = "FINDGROUPS"
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

                var dirgroups = new List<DirGroupInfo>();
                foreach (IValue iv in ((Map)m["RESULT"]).Values)
                {
                    if (iv is Map)
                    {
                        dirgroups.Add(iv.ToDirGroupInfo());
                    }
                }

                return dirgroups;
            }
        }
    }
}
