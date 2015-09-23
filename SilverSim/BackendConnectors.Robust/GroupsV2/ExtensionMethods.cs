// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public static class ExtensionMethods
    {
        #region Dir Group
        public static DirGroupInfo ToDirGroupInfo(this IValue iv)
        {
            Map m = (Map)iv;
            DirGroupInfo group = new DirGroupInfo();
            group.ID.ID = m["GroupID"].AsUUID;
            group.ID.GroupName = m["Name"].ToString();
            group.MemberCount = m["NMembers"].AsInt;
            group.SearchOrder = (float)(double)m["SearchOrder"].AsReal;

            return group;
        }
        #endregion

        #region Group
        public static GroupInfo ToGroup(this IValue iv, string serviceURI)
        {
            Map m = (Map)iv;
            GroupInfo group = new GroupInfo();
            if(m.ContainsKey("AllowPublish"))
            {
                group.IsAllowPublish = bool.Parse(m["AllowPublish"].ToString());
            }

            if(m.ContainsKey("Charter"))
            {
                group.Charter = m["Charter"].ToString();
            }

            if(m.ContainsKey("FounderUUI"))
            {
                group.Founder = new UUI(m["FounderUUI"].ToString());
            }
            else if(m.ContainsKey("FounderID"))
            {
                group.Founder.ID = m["FounderID"].AsUUID;
            }

            if(m.ContainsKey("GroupID"))
            {
                group.ID.ID = m["GroupID"].AsUUID;
            }

            if(m.ContainsKey("GroupName"))
            {
                group.ID.GroupName = m["GroupName"].ToString();
            }

            if(m.ContainsKey("InsigniaID"))
            {
                group.InsigniaID = m["InsigniaID"].AsUUID;
            }

            if(m.ContainsKey("MaturePublish"))
            {
                group.IsMaturePublish = bool.Parse(m["MaturePublish"].ToString());
            }

            if(m.ContainsKey("MembershipFee"))
            {
                group.MembershipFee = m["MembershipFee"].AsInt;
            }

            if(m.ContainsKey("OpenEnrollment"))
            {
                group.IsOpenEnrollment = bool.Parse(m["OpenEnrollment"].ToString());
            }

            if(m.ContainsKey("OwnerRoleID"))
            {
                group.OwnerRoleID = m["OwnerRoleID"].AsUUID;
            }

            if(m.ContainsKey("ServiceLocation"))
            {
                string uri = m["ServiceLocation"].ToString();
                if (Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                {
                    group.ID.HomeURI = new Uri(uri);
                }
            }
            if(group.ID.HomeURI == null)
            {
                group.ID.HomeURI = new Uri(serviceURI);
            }

            if(m.ContainsKey("ShownInList"))
            {
                group.IsShownInList = bool.Parse(m["ShownInList"].ToString());
            }

            if(m.ContainsKey("MemberCount"))
            {
                group.MemberCount = m["MemberCount"].AsInt;
            }

            if(m.ContainsKey("RoleCount"))
            {
                group.RoleCount = m["RoleCount"].AsInt;
            }

            return group;
        }

        public static Dictionary<string, string> ToPost(this GroupInfo group)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();

            post["AllowPublish"] = group.IsAllowPublish.ToString();
            post["Charter"] = group.Charter;
            post["FounderID"] = (string)group.Founder.ID;
            post["FounderUUI"] = group.Founder.ToString();
            post["GroupID"] = (string)group.ID.ID;
            post["GroupName"] = group.ID.GroupName;
            post["InsigniaID"] = (string)group.InsigniaID;
            post["MaturePublish"] = group.IsMaturePublish.ToString();
            post["MembershipFee"] = group.MembershipFee.ToString();
            post["OpenEnrollment"] = group.IsOpenEnrollment.ToString();
            post["OwnerRoleID"] = group.OwnerRoleID.ToString();
            post["ServiceLocation"] = group.ID.HomeURI != null ? group.ID.HomeURI.ToString() : "";
            post["ShownInList"] = group.IsShownInList.ToString();
            post["MemberCount"] = group.MemberCount.ToString();
            post["RoleCount"] = group.RoleCount.ToString();

            return post;
        }
        #endregion

        #region Group Member
        public static GroupMember ToGroupMemberFromMembership(this IValue iv)
        {
            Map m = (Map)iv;
            GroupMember member = new GroupMember();

            if(m.ContainsKey("AccessToken"))
            {
                member.AccessToken = m["AccessToken"].ToString();
            }

            if(m.ContainsKey("GroupID"))
            {
                member.Group.ID = m["GroupID"].AsUUID;
            }

            if(m.ContainsKey("GroupName"))
            {
                member.Group.GroupName = m["GroupName"].ToString();
            }

            if(m.ContainsKey("ActiveRole"))
            {
                member.SelectedRoleID = m["ActiveRole"].AsUUID;
            }

            if(m.ContainsKey("Contribution"))
            {
                member.Contribution = m["Contribution"].AsInt;
            }
            if(m.ContainsKey("ListInProfile"))
            {
                member.IsListInProfile = bool.Parse(m["ListInProfile"].ToString());
            }
            if(m.ContainsKey("AcceptNotices"))
            {
                member.IsAcceptNotices = bool.Parse(m["AcceptNotices"].ToString());
            }

            return member;
        }

        public static GroupMembership ToGroupMembership(this IValue iv)
        {
            Map m = (Map)iv;
            GroupMembership member = new GroupMembership();

            if (m.ContainsKey("GroupID"))
            {
                member.Group.ID = m["GroupID"].AsUUID;
            }

            if (m.ContainsKey("GroupName"))
            {
                member.Group.GroupName = m["GroupName"].ToString();
            }

            if (m.ContainsKey("GroupPicture"))
            {
                member.GroupInsigniaID = m["GroupPicture"].AsUUID;
            }

            if(m.ContainsKey("GroupPowers"))
            {
                member.GroupPowers = (GroupPowers)ulong.Parse(m["GroupPowers"].ToString());
            }

            if (m.ContainsKey("Contribution"))
            {
                member.Contribution = m["Contribution"].AsInt;
            }
            if (m.ContainsKey("ListInProfile"))
            {
                member.ListInProfile = bool.Parse(m["ListInProfile"].ToString());
            }
            if (m.ContainsKey("AcceptNotices"))
            {
                member.AcceptNotices = bool.Parse(m["AcceptNotices"].ToString());
            }

            return member;
        }

        public static GroupMember ToGroupMember(this IValue iv, UGI group)
        {
            Map m = (Map)iv;
            GroupMember member = new GroupMember();
            member.Group = group;

            if(m.ContainsKey("AcceptNotices"))
            {
                member.IsAcceptNotices = bool.Parse(m["AcceptNotices"].ToString());
            }

            if(m.ContainsKey("AccessToken"))
            {
                member.AccessToken = m["AccessToken"].ToString();
            }

            if(m.ContainsKey("AgentID"))
            {
                member.Principal = new UUI(m["AgentID"].ToString());
            }

            if(m.ContainsKey("Contribution"))
            {
                member.Contribution = m["Contribution"].AsInt;
            }

            if(m.ContainsKey("ListInProfile"))
            {
                member.IsListInProfile = bool.Parse(m["ListInProfile"].ToString());
            }

            return member;
        }
        #endregion

        #region GroupRole
        public static GroupRole ToGroupRole(this IValue iv)
        {
            Map m = (Map)iv;
            GroupRole role = new GroupRole();
            if(m.ContainsKey("Description"))
            {
                role.Description = m["Description"].ToString();
            }
            if(m.ContainsKey("Members"))
            {
                role.Members = m["Members"].AsUInt;
            }
            if(m.ContainsKey("Name"))
            {
                role.Name = m["Name"].ToString();
            }

            if(m.ContainsKey("Powers"))
            {
                role.Powers = (GroupPowers)m["Powers"].AsULong;
            }
            
            if(m.ContainsKey("Title"))
            {
                role.Title = m["Title"].ToString();
            }

            role.ID = m["RoleID"].AsUUID;

            return role;
        }

        public static Dictionary<string, string> ToPost(this GroupRole role)
        {
            Dictionary<string, string> m = new Dictionary<string, string>();
            m.Add("GroupID", (string)role.Group.ID);
            m.Add("RoleID", (string)role.ID);
            m.Add("Name", role.Name);
            m.Add("Description", role.Description);
            m.Add("Title", role.Title);
            m.Add("Powers", ((ulong)role.Powers).ToString());
            return m;
        }
        #endregion

        #region Group Rolemember
        public static GroupRolemember ToGroupRolemember(this IValue iv)
        {
            Map m = (Map)iv;
            GroupRolemember member = new GroupRolemember();
            member.RoleID = m["RoleID"].AsUUID;
            member.Principal = new UUI(m["MemberID"].ToString());
            return member;
        }

        public static Dictionary<string, string> ToPost(this GroupRolemember m, RobustGroupsConnector.GetGroupsAgentIDDelegate getGroupsAgentID)
        {
            Dictionary<string, string> post = new Dictionary<string,string>();
            post["RoleID"] = (string)m.RoleID;
            post["MemberID"] = getGroupsAgentID(m.Principal);

            return post;
        }
        #endregion

        #region Group Notice
        public static GroupNotice ToGroupNotice(this IValue iv)
        {
            Map m = (Map)iv;

            GroupNotice notice = new GroupNotice();
            notice.ID = m["NoticeID"].AsUUID;
            notice.Timestamp = Date.UnixTimeToDateTime(m["Timestamp"].AsULong);
            notice.FromName = m["FromName"].ToString();
            notice.Subject = m["Subject"].ToString();
            notice.HasAttachment = bool.Parse(m["HasAttachment"].ToString());
            if (notice.HasAttachment)
            {
                notice.AttachmentItemID = m["AttachmentItemID"].AsUUID;
                notice.AttachmentName = m["AttachmentName"].ToString();
                notice.AttachmentType = (AssetType)m["AttachmentType"].AsInt;
                if ("" != m["AttachmentOwnerID"].ToString())
                {
                    notice.AttachmentOwner = new UUI(m["AttachmentOwnerID"].ToString());
                }
            }
            return notice;
        }

        public static Dictionary<string, string> ToPost(this GroupNotice notice, RobustGroupsConnector.GetGroupsAgentIDDelegate getGroupsAgentID)
        {
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["NoticeID"] = (string)notice.ID;
            post["Timestamp"] = notice.Timestamp.AsULong.ToString();
            post["FromName"] = notice.FromName;
            post["Subject"] = notice.Subject;
            post["HasAttachment"] = notice.HasAttachment.ToString();
            if (notice.HasAttachment)
            {
                post["AttachmentItemID"] = (string)notice.AttachmentItemID;
                post["AttachmentName"] = notice.AttachmentName;
                post["AttachmentType"] = ((int)notice.AttachmentType).ToString();
                if (notice.AttachmentOwner != null && notice.AttachmentOwner.ID != UUID.Zero)
                {
                    post["AttachmentOwnerID"] = getGroupsAgentID(notice.AttachmentOwner);
                }
                else
                {
                    post["AttachmentOwnerID"] = "";
                }
            }
            return post;
        }
        #endregion
    }
}
