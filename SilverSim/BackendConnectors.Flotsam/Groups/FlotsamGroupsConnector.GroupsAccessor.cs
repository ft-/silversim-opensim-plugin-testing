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

using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector : GroupsServiceInterface.IGroupsInterface
    {
        GroupInfo IGroupsInterface.Create(UGUI requestingAgent, GroupInfo group)
        {
            var m = new Map
            {
                { "GroupID", group.ID.ID },
                { "Name", group.ID.GroupName },
                { "Charter", group.Charter },
                { "InsigniaID", group.InsigniaID },
                { "FounderID", group.Founder.ID },
                { "MembershipFee", group.MembershipFee },
                { "OpenEnrollment", group.IsOpenEnrollment.ToString() },
                { "ShowInList", group.IsShownInList ? 1 : 0 },
                { "AllowPublish", group.IsAllowPublish ? 1 : 0 },
                { "MaturePublish", group.IsMaturePublish ? 1 : 0 },
                { "OwnerRoleID", group.OwnerRoleID },
                { "EveryonePowers", ((ulong)GroupPowers.DefaultEveryonePowers).ToString() },
                { "OwnersPowers", ((ulong)GroupPowers.OwnerPowers).ToString() }
            };
            var res = FlotsamXmlRpcCall(requestingAgent, "groups.createGroup", m) as Map;
            if(res == null)
            {
                throw new InvalidDataException();
            }
            return res.ToGroupInfo(m_AvatarNameService);
        }

        GroupInfo IGroupsInterface.Update(UGUI requestingAgent, GroupInfo group)
        {
            var m = new Map
            {
                { "GroupID", group.ID.ID },
                { "Charter", group.Charter },
                { "InsigniaID", group.InsigniaID },
                { "MembershipFee", group.MembershipFee },
                { "OpenEnrollment", group.IsOpenEnrollment.ToString() },
                { "ShowInList", group.IsShownInList ? 1 : 0 },
                { "AllowPublish", group.IsAllowPublish ? 1 : 0 },
                { "MaturePublish", group.IsMaturePublish ? 1 : 0 }
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.updateGroup", m);
            return Groups[requestingAgent, group.ID];
        }

        void IGroupsInterface.Delete(UGUI requestingAgent, UGI group)
        {
            throw new NotImplementedException();
        }

        bool IGroupsInterface.TryGetValue(UGUI requestingAgent, UUID groupID, out UGI ugi)
        {
            var m = new Map
            {
                ["GroupID"] = groupID
            };
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
            if (m == null)
            {
                ugi = default(UGI);
                return false;
            }
            ugi = m.ToGroupInfo(m_AvatarNameService).ID;
            return true;
        }

        bool IGroupsInterface.ContainsKey(UGUI requestingAgent, UUID groupID)
        {
            var m = new Map
            {
                ["GroupID"] = groupID
            };
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
            if (m == null)
            {
                return false;
            }
            return true;
        }

        UGI IGroupsInterface.this[UGUI requestingAgent, UUID groupID]
        {
            get
            {
                var m = new Map
                {
                    ["GroupID"] = groupID
                };
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                if(m == null)
                {
                    throw new InvalidDataException();
                }
                return m.ToGroupInfo(m_AvatarNameService).ID;
            }
        }

        bool IGroupsInterface.TryGetValue(UGUI requestingAgent, UGI group, out GroupInfo groupInfo)
        {
            var m = new Map
            {
                ["GroupID"] = group.ID
            };
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
            if (m == null)
            {
                groupInfo = default(GroupInfo);
                return false;
            }
            groupInfo = m.ToGroupInfo(m_AvatarNameService);
            return true;
        }

        bool IGroupsInterface.ContainsKey(UGUI requestingAgent, UGI group)
        {
            var m = new Map
            {
                ["GroupID"] = group.ID
            };
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
            if (m == null)
            {
                return false;
            }
            return true;
        }

        GroupInfo IGroupsInterface.this[UGUI requestingAgent, UGI group]
        {
            get
            {
                var m = new Map
                {
                    ["GroupID"] = group.ID
                };
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                if(m == null)
                {
                    throw new InvalidDataException();
                }
                return m.ToGroupInfo(m_AvatarNameService);
            }
        }

        bool IGroupsInterface.TryGetValue(UGUI requestingAgent, string groupName, out GroupInfo groupInfo)
        {
            var m = new Map
            {
                { "Name", groupName }
            };
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
            if (m == null)
            {
                groupInfo = default(GroupInfo);
                return false;
            }
            groupInfo = m.ToGroupInfo(m_AvatarNameService);
            return true;
        }

        bool IGroupsInterface.ContainsKey(UGUI requestingAgent, string groupName)
        {
            var m = new Map
            {
                { "Name", groupName }
            };
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
            if (m == null)
            {
                return false;
            }
            return true;
        }

        GroupInfo IGroupsInterface.this[UGUI requestingAgent, string groupName]
        {
            get
            {
                var m = new Map
                {
                    { "Name", groupName }
                };
                m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
                if(m == null)
                {
                    throw new InvalidDataException();
                }
                return m.ToGroupInfo(m_AvatarNameService);
            }
        }

        List<DirGroupInfo> IGroupsInterface.GetGroupsByName(UGUI requestingAgent, string query)
        {
            var m = new Map
            {
                { "Search", query }
            };
            var results = (AnArray)FlotsamXmlRpcCall(requestingAgent, "groups.findGroups", m);

            var groups = new List<DirGroupInfo>();
            foreach(IValue iv in results)
            {
                var data = iv as Map;
                if (data != null)
                {
                    groups.Add(data.ToDirGroupInfo());
                }
            }

            return groups;
        }
    }
}
