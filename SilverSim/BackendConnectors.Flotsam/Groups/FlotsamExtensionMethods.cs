// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public static class FlotsamExtensionMethods
    {
        public static GroupInfo ToGroupInfo(this Map m, AvatarNameServiceInterface avatarNameService)
        {
            GroupInfo gi = new GroupInfo();
            gi.ID.ID = m["GroupID"].AsUUID;
            gi.ID.GroupName = m["Name"].ToString();
            gi.Charter = m["Charter"].ToString();
            gi.InsigniaID = m["InsigniaID"].AsUUID;
            gi.Founder.ID = m["FounderID"].AsUUID;
            gi.MembershipFee = m["MembershipFee"].AsInt;
            gi.IsOpenEnrollment = Convert.ToBoolean(m["OpenEnrollment"].ToString());
            gi.IsShownInList = Convert.ToBoolean(m["ShowInList"].ToString());
            gi.IsAllowPublish = Convert.ToBoolean(m["AllowPublish"].ToString());
            gi.IsMaturePublish = Convert.ToBoolean(m["MaturePublish"].ToString());
            gi.OwnerRoleID = m["OwnerRoleID"].AsUUID;
            gi.Founder = avatarNameService.ResolveName(gi.Founder);
            return gi;
        }

        public static DirGroupInfo ToDirGroupInfo(this Map m)
        {
            DirGroupInfo gi = new DirGroupInfo();
            gi.MemberCount = m["Members"].AsInt;
            gi.ID.GroupName = m["Name"].ToString();
            gi.ID.ID = m["GroupID"].AsUUID;
            return gi;
        }

        public static GroupMember ToGroupMember(this Map m, UGI group, AvatarNameServiceInterface avatarNameService)
        {
            GroupMember gmem = new GroupMember();
            gmem.IsListInProfile = Convert.ToBoolean(m["ListInProfile"].ToString());
            gmem.Contribution = m["Contribution"].AsInt;
            gmem.IsAcceptNotices = Convert.ToBoolean(m["AcceptNotices"].ToString());
            gmem.SelectedRoleID = m["SelectedRoleID"].AsUUID;
            gmem.Principal.ID = m["AgentID"].AsUUID;
            gmem.Group = group;
            gmem.Principal = avatarNameService.ResolveName(gmem.Principal);
            return gmem;
        }

        public static GroupMember ToGroupMember(this Map m, AvatarNameServiceInterface avatarNameService)
        {
            GroupMember gmem = new GroupMember();
            gmem.IsListInProfile = Convert.ToBoolean(m["ListInProfile"].ToString());
            gmem.Contribution = m["Contribution"].AsInt;
            gmem.IsAcceptNotices = Convert.ToBoolean(m["AcceptNotices"].ToString());
            gmem.SelectedRoleID = m["SelectedRoleID"].AsUUID;
            gmem.Principal.ID = m["AgentID"].AsUUID;
            gmem.Principal = avatarNameService.ResolveName(gmem.Principal);
            gmem.Group.ID = m["GroupID"].AsUUID;
            gmem.Group.GroupName = m["GroupName"].ToString();
            return gmem;
        }

        public static GroupMembership ToGroupMembership(this Map m, AvatarNameServiceInterface avatarNameService)
        {
            GroupMembership gmem = new GroupMembership();
            gmem.IsListInProfile = Convert.ToBoolean(m["ListInProfile"].ToString());
            gmem.Contribution = m["Contribution"].AsInt;
            gmem.IsAcceptNotices = Convert.ToBoolean(m["AcceptNotices"].ToString());
            gmem.GroupTitle = m["Title"].ToString();
            gmem.GroupPowers = (GroupPowers)ulong.Parse(m["GroupPowers"].ToString());
            gmem.Principal.ID = m["AgentID"].AsUUID;
            gmem.Principal = avatarNameService.ResolveName(gmem.Principal);
            gmem.Group.ID = m["GroupID"].AsUUID;
            gmem.Group.GroupName = m["GroupName"].ToString();
            gmem.IsAllowPublish = Convert.ToBoolean(m["AllowPublish"].ToString());
            gmem.Charter = m["Charter"].ToString();
            gmem.ActiveRoleID = m["SelectedRoleID"].ToString();
            gmem.Founder.ID = m["FounderID"].ToString();
            gmem.Founder = avatarNameService.ResolveName(gmem.Founder);
            gmem.AccessToken = string.Empty;
            gmem.IsMaturePublish = Convert.ToBoolean(m["MaturePublish"].ToString());
            gmem.IsOpenEnrollment = Convert.ToBoolean(m["OpenEnrollment"].ToString());
            gmem.MembershipFee = int.Parse(m["MembershipFee"].ToString());
            gmem.IsShownInList = Convert.ToBoolean(m["OPenEnrollment"].ToString());

            return gmem;
        }

        public static GroupRolemember ToGroupRolemember(this Map m, UGI group, AvatarNameServiceInterface avatarNameService)
        {
            GroupRolemember gmem = new GroupRolemember();
            gmem.RoleID = m["RoleID"].AsUUID;
            gmem.Principal.ID = m["AgentID"].AsUUID;
            gmem.Group = group;
            gmem.Principal = avatarNameService.ResolveName(gmem.Principal);
            return gmem;
        }

        public static GroupRolemembership ToGroupRolemembership(this Map m, UGI group, AvatarNameServiceInterface avatarNameService)
        {
            GroupRolemembership gmem = new GroupRolemembership();
            gmem.RoleID = m["RoleID"].AsUUID;
            gmem.Principal.ID = m["AgentID"].AsUUID;
            gmem.GroupTitle = m["Title"].ToString();
            gmem.Group = group;
            gmem.Principal = avatarNameService.ResolveName(gmem.Principal);
            return gmem;
        }

        public static GroupRolemembership ToGroupRolemembership(this Map m, AvatarNameServiceInterface avatarNameService)
        {
            GroupRolemembership gmem = new GroupRolemembership();
            gmem.RoleID = m["RoleID"].AsUUID;
            gmem.Principal.ID = m["AgentID"].AsUUID;
            gmem.GroupTitle = m["Title"].ToString();
            gmem.Group.ID = m["GroupID"].AsUUID;
            gmem.Principal = avatarNameService.ResolveName(gmem.Principal);
            return gmem;
        }

        public static GroupRole ToGroupRole(this Map m, UGI group)
        {
            GroupRole role = new GroupRole();
            role.Group = group;
            role.ID = m["RoleID"].AsUUID;
            role.Members = m["Members"].AsUInt;
            role.Name = m["Name"].ToString();
            role.Description = m["Description"].ToString();
            role.Title = m["Title"].ToString();
            role.Powers = (GroupPowers)m["Powers"].AsULong;
            return role;
        }

        public static GroupNotice ToGroupNotice(this Map m)
        {
            GroupNotice notice = new GroupNotice();
            notice.Group.ID = m["GroupID"].AsUUID;
            notice.ID = m["NoticeID"].AsUUID;
            notice.Timestamp = Date.UnixTimeToDateTime(m["Timestamp"].AsULong);
            notice.FromName = m["FromName"].ToString();
            notice.Subject = m["Subject"].ToString();
            notice.Message = m["Message"].ToString();
#warning TODO: Implement BinaryBucket conversion
            return notice;
        }
    }
}
