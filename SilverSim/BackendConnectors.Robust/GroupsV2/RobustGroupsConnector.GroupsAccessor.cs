// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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
        public sealed class GroupsAccessor : IGroupsInterface
        {
            public int TimeoutMs = 20000;
            readonly string m_GroupServiceURI;
            readonly string m_Uri;
            readonly Func<UUI, string> m_GetGroupsAgentID;

            public GroupsAccessor(string uri, string serviceURI, Func<UUI, string> getGroupsAgentID)
            {
                m_Uri = uri;
                m_GroupServiceURI = serviceURI;
                m_GetGroupsAgentID = getGroupsAgentID;
            }

            GroupInfo CreateOrUpdate(UUI requestingAgent, GroupInfo group, string op)
            {
                Dictionary<string, string> post = group.ToPost();
                post.Remove("OwnerRoleID");
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["OP"] = op;
                post["METHOD"] = "PUTGROUP";
                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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

                return m["RESULT"].ToGroup(m_GroupServiceURI);
            }

            public GroupInfo Create(UUI requestingAgent, GroupInfo group)
            {
                return CreateOrUpdate(requestingAgent, group, "ADD");
            }

            public GroupInfo Update(UUI requestingAgent, GroupInfo group)
            {
                return CreateOrUpdate(requestingAgent, group, "UPDATE");
            }

            public void Delete(UUI requestingAgent, GroupInfo group)
            {
                throw new NotSupportedException();
            }

            public bool TryGetValue(UUI requestingAgent, UUID groupID, out UGI ugi)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["GroupID"] = (string)groupID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETGROUP";
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

                ugi = m["RESULT"].ToGroup(m_GroupServiceURI).ID;
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, UUID groupID)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["GroupID"] = (string)groupID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETGROUP";
                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["GroupID"] = (string)group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETGROUP";
                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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

                groupInfo = m["RESULT"].ToGroup(m_GroupServiceURI);
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, UGI group)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["GroupID"] = (string)group.ID;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETGROUP";
                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["Name"] = groupName;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETGROUP";
                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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

                groupInfo = m["RESULT"].ToGroup(m_GroupServiceURI);
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, string groupName)
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["Name"] = groupName;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "GETGROUP";
                Map m;
                using (Stream s = HttpRequestHandler.DoStreamPostRequest(m_Uri, null, post, false, TimeoutMs))
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
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["Query"] = query;
                post["RequestingAgentID"] = m_GetGroupsAgentID(requestingAgent);
                post["METHOD"] = "FINDGROUPS";
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

                List<DirGroupInfo> dirgroups = new List<DirGroupInfo>();
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
