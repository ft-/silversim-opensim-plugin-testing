﻿// SilverSim is distributed under the terms of the
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
        GroupInfo IGroupsInterface.Create(UUI requestingAgent, GroupInfo group)
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
            return res.ToGroupInfo(m_AvatarNameService);
        }

        GroupInfo IGroupsInterface.Update(UUI requestingAgent, GroupInfo group)
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
            return Groups[requestingAgent, group.ID];
        }

        void IGroupsInterface.Delete(UUI requestingAgent, UGI group)
        {
            throw new NotImplementedException();
        }

        bool IGroupsInterface.TryGetValue(UUI requestingAgent, UUID groupID, out UGI ugi)
        {
            Map m = new Map();
            m.Add("GroupID", groupID);
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
            if (null == m)
            {
                ugi = default(UGI);
                return false;
            }
            ugi = m.ToGroupInfo(m_AvatarNameService).ID;
            return true;
        }

        bool IGroupsInterface.ContainsKey(UUI requestingAgent, UUID groupID)
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

        UGI IGroupsInterface.this[UUI requestingAgent, UUID groupID]
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
                return m.ToGroupInfo(m_AvatarNameService).ID;
            }
        }

        bool IGroupsInterface.TryGetValue(UUI requestingAgent, UGI group, out GroupInfo groupInfo)
        {
            Map m = new Map();
            m.Add("GroupID", group.ID);
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
            if (null == m)
            {
                groupInfo = default(GroupInfo);
                return false;
            }
            groupInfo = m.ToGroupInfo(m_AvatarNameService);
            return true;
        }

        bool IGroupsInterface.ContainsKey(UUI requestingAgent, UGI group)
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

        GroupInfo IGroupsInterface.this[UUI requestingAgent, UGI group]
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
                return m.ToGroupInfo(m_AvatarNameService);
            }
        }

        bool IGroupsInterface.TryGetValue(UUI requestingAgent, string groupName, out GroupInfo groupInfo)
        {
            Map m = new Map();
            m.Add("Name", groupName);
            m = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroup", m) as Map;
            if (null == m)
            {
                groupInfo = default(GroupInfo);
                return false;
            }
            groupInfo = m.ToGroupInfo(m_AvatarNameService);
            return true;
        }

        bool IGroupsInterface.ContainsKey(UUI requestingAgent, string groupName)
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

        GroupInfo IGroupsInterface.this[UUI requestingAgent, string groupName]
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
                return m.ToGroupInfo(m_AvatarNameService);
            }
        }

        List<DirGroupInfo> IGroupsInterface.GetGroupsByName(UUI requestingAgent, string query)
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
