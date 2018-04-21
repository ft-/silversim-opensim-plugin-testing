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

using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public static class ExtensionMethods
    {
        #region Dir Group
        public static DirGroupInfo ToDirGroupInfo(this IValue iv)
        {
            Map m = (Map)iv;
            return new DirGroupInfo
            {
                ID = new UGI { ID = m["GroupID"].AsUUID, GroupName = m["Name"].ToString() },
                MemberCount = m["NMembers"].AsInt,
                SearchOrder = (float)(double)m["SearchOrder"].AsReal
            };
        }
        #endregion

        #region Group
        public static GroupInfo ToGroup(this IValue iv)
        {
            var m = (Map)iv;
            var group = new GroupInfo();
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
                group.Founder = new UGUI(m["FounderUUI"].ToString());
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

        public static Dictionary<string, string> ToPost(this GroupInfo group) => new Dictionary<string, string>
        {
            ["AllowPublish"] = group.IsAllowPublish.ToString(),
            ["Charter"] = group.Charter,
            ["FounderID"] = (string)group.Founder.ID,
            ["FounderUUI"] = group.Founder.ToString(),
            ["GroupID"] = (string)group.ID.ID,
            ["Name"] = group.ID.GroupName,
            ["InsigniaID"] = (string)group.InsigniaID,
            ["MaturePublish"] = group.IsMaturePublish.ToString(),
            ["MembershipFee"] = group.MembershipFee.ToString(),
            ["OpenEnrollment"] = group.IsOpenEnrollment.ToString(),
            ["OwnerRoleID"] = group.OwnerRoleID.ToString(),
            ["ServiceLocation"] = group.ID.HomeURI != null ? group.ID.HomeURI.ToString() : string.Empty,
            ["ShownInList"] = group.IsShownInList.ToString(),
            ["MemberCount"] = group.MemberCount.ToString(),
            ["RoleCount"] = group.RoleCount.ToString()
        };
        #endregion

        #region Group Member
        public static GroupMember ToGroupMemberFromMembership(this IValue iv)
        {
            var m = (Map)iv;
            var member = new GroupMember();

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
            var m = (Map)iv;
            var member = new GroupMembership();

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
                member.IsListInProfile = bool.Parse(m["ListInProfile"].ToString());
            }
            if (m.ContainsKey("AcceptNotices"))
            {
                member.IsAcceptNotices = bool.Parse(m["AcceptNotices"].ToString());
            }
            if(m.ContainsKey("AllowPublish"))
            {
                member.IsAllowPublish = bool.Parse(m["AllowPublish"].ToString());
            }
            if(m.ContainsKey("Charter"))
            {
                member.Charter = m["Charter"].ToString();
            }
            if(m.ContainsKey("ActiveRole"))
            {
                member.ActiveRoleID = m["ActiveRole"].ToString();
            }
            if(m.ContainsKey("FounderID"))
            {
                member.Founder.ID = m["FounderID"].ToString();
            }
            if(m.ContainsKey("AccessToken"))
            {
                member.AccessToken = m["AccessToken"].ToString();
            }
            if(m.ContainsKey("MaturePublish"))
            {
                member.IsMaturePublish = bool.Parse(m["MaturePublish"].ToString());
            }
            if(m.ContainsKey("OpenEnrollment"))
            {
                member.IsOpenEnrollment = bool.Parse(m["OpenEnrollment"].ToString());
            }
            if(m.ContainsKey("MembershipFee"))
            {
                member.MembershipFee = int.Parse(m["MembershipFee"].ToString());
            }
            if(m.ContainsKey("ShowInList"))
            {
                member.IsShownInList = bool.Parse(m["ShowInList"].ToString());
            }

            return member;
        }

        public static GroupMember ToGroupMember(this IValue iv, UGI group)
        {
            var m = (Map)iv;
            var member = new GroupMember();
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
                member.Principal = new UGUI(m["AgentID"].ToString());
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
            var m = (Map)iv;
            var role = new GroupRole();
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

        public static Dictionary<string, string> ToPost(this GroupRole role) => new Dictionary<string, string>
        {
            { "GroupID", (string)role.Group.ID },
            { "RoleID", (string)role.ID },
            { "Name", role.Name },
            { "Description", role.Description },
            { "Title", role.Title },
            { "Powers", ((ulong)role.Powers).ToString() }
        };
        #endregion

        #region Group Rolemember
        public static GroupRolemember ToGroupRolemember(this IValue iv)
        {
            var m = (Map)iv;
            return new GroupRolemember
            {
                RoleID = m["RoleID"].AsUUID,
                Principal = new UGUI(m["MemberID"].ToString())
            };
        }

        public static Dictionary<string, string> ToPost(this GroupRolemember m, Func<UGUI, string> getGroupsAgentID) => new Dictionary<string, string>
        {
            ["RoleID"] = (string)m.RoleID,
            ["MemberID"] = getGroupsAgentID(m.Principal)
        };
        #endregion

        #region Group Notice
        public static GroupNotice ToGroupNotice(this IValue iv)
        {
            var m = (Map)iv;

            var notice = new GroupNotice
            {
                ID = m["NoticeID"].AsUUID,
                Timestamp = Date.UnixTimeToDateTime(m["Timestamp"].AsULong),
                FromName = m["FromName"].ToString(),
                Subject = m["Subject"].ToString(),
                HasAttachment = bool.Parse(m["HasAttachment"].ToString())
            };
            if (notice.HasAttachment)
            {
                notice.AttachmentItemID = m["AttachmentItemID"].AsUUID;
                notice.AttachmentName = m["AttachmentName"].ToString();
                notice.AttachmentType = (AssetType)m["AttachmentType"].AsInt;
                string attachOwnerId = m["AttachmentOwnerID"].ToString();
                if (0 != attachOwnerId.Length)
                {
                    notice.AttachmentOwner = new UGUI(attachOwnerId);
                }
            }
            return notice;
        }

        public static Dictionary<string, string> ToPost(this GroupNotice notice, Func<UGUI, string> getGroupsAgentID)
        {
            var post = new Dictionary<string, string>
            {
                ["NoticeID"] = (string)notice.ID,
                ["Timestamp"] = notice.Timestamp.AsULong.ToString(),
                ["FromName"] = notice.FromName,
                ["Subject"] = notice.Subject,
                ["HasAttachment"] = notice.HasAttachment.ToString()
            };
            if (notice.HasAttachment)
            {
                post["AttachmentItemID"] = (string)notice.AttachmentItemID;
                post["AttachmentName"] = notice.AttachmentName;
                post["AttachmentType"] = ((int)notice.AttachmentType).ToString();
                post["AttachmentOwnerID"] = (notice.AttachmentOwner != null && notice.AttachmentOwner.ID != UUID.Zero) ?
                        getGroupsAgentID(notice.AttachmentOwner) :
                        string.Empty;
            }
            return post;
        }
        #endregion
    }
}
