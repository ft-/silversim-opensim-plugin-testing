// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector
    {
        public sealed class GroupsAccessor : FlotsamGroupsCommonConnector, IGroupsInterface
        {
            public GroupsAccessor(string uri)
                : base(uri)
            {
            }

            public GroupInfo Create(UUI requestingAgent, GroupInfo group)
            {
                Map m = new Map();
                m.Add("GroupID", group.ID.ID);
                m.Add("Name", group.ID.GroupName);
                m.Add("Charter", group.Charter);
                m.Add("InsigniaID", group.InsigniaID);
                m.Add("FounderID", group.Founder.ID);
                m.Add("MembershipFee", group.MembershipFee);
                m.Add("OpenEnrollment", group.IsOpenEnrollment.ToString());
                m.Add("ShowInList", group.IsShownInList ? 1 : 0);
                m.Add("AllowPublish", group.IsAllowPublish ? 1 : 0);
                m.Add("MaturePublish", group.IsMaturePublish ? 1 : 0);
                m.Add("OwnerRoleID", group.OwnerRoleID);
                m.Add("EveryonePowers", ((ulong)GroupPowers.DefaultEveryonePowers).ToString());
                m.Add("OwnersPowers", ((ulong)GroupPowers.OwnerPowers).ToString());
                Map res = FlotsamXmlRpcCall(requestingAgent, "groups.createGroup", m) as Map;
                if(null == res)
                {
                    throw new InvalidDataException();
                }
                return res.ToGroupInfo();
            }

            public GroupInfo Update(UUI requestingAgent, GroupInfo group)
            {
                Map m = new Map();
                m.Add("GroupID", group.ID.ID);
                m.Add("Charter", group.Charter);
                m.Add("InsigniaID", group.InsigniaID);
                m.Add("MembershipFee", group.MembershipFee);
                m.Add("OpenEnrollment", group.IsOpenEnrollment.ToString());
                m.Add("ShowInList", group.IsShownInList ? 1 : 0);
                m.Add("AllowPublish", group.IsAllowPublish ? 1 : 0);
                m.Add("MaturePublish", group.IsMaturePublish ? 1 : 0);
                FlotsamXmlRpcCall(requestingAgent, "groups.updateGroup", m);
                return this[requestingAgent, group.ID];
            }

            public void Delete(UUI requestingAgent, UGI group)
            {
                throw new NotImplementedException();
            }

            public bool TryGetValue(UUI requestingAgent, UUID groupID, out UGI ugi)
            {
                Map m = new Map();
                m.Add("GroupID", groupID);
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                if (null == m)
                {
                    ugi = default(UGI);
                    return false;
                }
                ugi = m.ToGroupInfo().ID;
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, UUID groupID)
            {
                Map m = new Map();
                m.Add("GroupID", groupID);
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                if (null == m)
                {
                    return false;
                }
                return true;
            }

            public UGI this[UUI requestingAgent, UUID groupID]
            {
                get
                {
                    Map m = new Map();
                    m.Add("GroupID", groupID);
                    m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                    if(null == m)
                    {
                        throw new InvalidDataException();
                    }
                    return m.ToGroupInfo().ID;
                }
            }

            public bool TryGetValue(UUI requestingAgent, UGI group, out GroupInfo groupInfo)
            {
                Map m = new Map();
                m.Add("GroupID", group.ID);
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                if (null == m)
                {
                    groupInfo = default(GroupInfo);
                    return false;
                }
                groupInfo = m.ToGroupInfo();
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, UGI group)
            {
                Map m = new Map();
                m.Add("GroupID", group.ID);
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                if (null == m)
                {
                    return false;
                }
                return true;
            }

            public GroupInfo this[UUI requestingAgent, UGI group]
            {
                get 
                {
                    Map m = new Map();
                    m.Add("GroupID", group.ID);
                    m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                    if(null == m)
                    {
                        throw new InvalidDataException();
                    }
                    return m.ToGroupInfo();
                }
            }

            public bool TryGetValue(UUI requestingAgent, string groupName, out GroupInfo groupInfo)
            {
                Map m = new Map();
                m.Add("Name", groupName);
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                if (null == m)
                {
                    groupInfo = default(GroupInfo);
                    return false;
                }
                groupInfo = m.ToGroupInfo();
                return true;
            }

            public bool ContainsKey(UUI requestingAgent, string groupName)
            {
                Map m = new Map();
                m.Add("Name", groupName);
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                if (null == m)
                {
                    return false;
                }
                return true;
            }

            public GroupInfo this[UUI requestingAgent, string groupName]
            {
                get 
                {
                    Map m = new Map();
                    m.Add("Name", groupName);
                    m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                    if(null == m)
                    {
                        throw new InvalidDataException();
                    }
                    return m.ToGroupInfo();
                }
            }

            public List<DirGroupInfo> GetGroupsByName(UUI requestingAgent, string query)
            {
                Map m = new Map();
                m.Add("Search", query);
                AnArray results = (AnArray)FlotsamXmlRpcCall(requestingAgent, "groups.findGroups", m);

                List<DirGroupInfo> groups = new List<DirGroupInfo>();
                foreach(IValue iv in results)
                {
                    Map data = iv as Map;
                    if (null != data)
                    {
                        groups.Add(data.ToDirGroupInfo());
                    }
                }

                return groups;
            }
        }
    }
}
