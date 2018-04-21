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

using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public static class FlotsamExtensionMethods
    {
        public static GroupInfo ToGroupInfo(this Map m, AvatarNameServiceInterface avatarNameService) => new GroupInfo
        {
            ID = new UGI { ID = m["GroupID"].AsUUID, GroupName = m["Name"].ToString() },
            Charter = m["Charter"].ToString(),
            InsigniaID = m["InsigniaID"].AsUUID,
            Founder = avatarNameService.ResolveName(new UGUI(m["FounderID"].AsUUID)),
            MembershipFee = m["MembershipFee"].AsInt,
            IsOpenEnrollment = Convert.ToBoolean(m["OpenEnrollment"].ToString()),
            IsShownInList = Convert.ToBoolean(m["ShowInList"].ToString()),
            IsAllowPublish = Convert.ToBoolean(m["AllowPublish"].ToString()),
            IsMaturePublish = Convert.ToBoolean(m["MaturePublish"].ToString()),
            OwnerRoleID = m["OwnerRoleID"].AsUUID
        };

        public static DirGroupInfo ToDirGroupInfo(this Map m) => new DirGroupInfo
        {
            MemberCount = m["Members"].AsInt,
            ID = new UGI { GroupName = m["Name"].ToString(), ID = m["GroupID"].AsUUID }
        };

        public static GroupMember ToGroupMember(this Map m, UGI group, AvatarNameServiceInterface avatarNameService) => new GroupMember
        {
            IsListInProfile = Convert.ToBoolean(m["ListInProfile"].ToString()),
            Contribution = m["Contribution"].AsInt,
            IsAcceptNotices = Convert.ToBoolean(m["AcceptNotices"].ToString()),
            SelectedRoleID = m["SelectedRoleID"].AsUUID,
            Principal = avatarNameService.ResolveName(new UGUI(m["AgentID"].AsUUID)),
            Group = group
        };

        public static GroupMember ToGroupMember(this Map m, AvatarNameServiceInterface avatarNameService) => new GroupMember
        {
            IsListInProfile = Convert.ToBoolean(m["ListInProfile"].ToString()),
            Contribution = m["Contribution"].AsInt,
            IsAcceptNotices = Convert.ToBoolean(m["AcceptNotices"].ToString()),
            SelectedRoleID = m["SelectedRoleID"].AsUUID,
            Principal = avatarNameService.ResolveName(new UGUI(m["AgentID"].AsUUID)),
            Group = new UGI { ID = m["GroupID"].AsUUID, GroupName = m["GroupName"].ToString() }
        };

        public static GroupMembership ToGroupMembership(this Map m, AvatarNameServiceInterface avatarNameService) => new GroupMembership
        {
            IsListInProfile = Convert.ToBoolean(m["ListInProfile"].ToString()),
            Contribution = m["Contribution"].AsInt,
            IsAcceptNotices = Convert.ToBoolean(m["AcceptNotices"].ToString()),
            GroupTitle = m["Title"].ToString(),
            GroupPowers = (GroupPowers)ulong.Parse(m["GroupPowers"].ToString()),
            Principal = avatarNameService.ResolveName(new UGUI(m["AgentID"].AsUUID)),
            Group = new UGI { ID = m["GroupID"].AsUUID, GroupName = m["GroupName"].ToString() },
            IsAllowPublish = Convert.ToBoolean(m["AllowPublish"].ToString()),
            Charter = m["Charter"].ToString(),
            ActiveRoleID = m["SelectedRoleID"].ToString(),
            Founder = avatarNameService.ResolveName(new UGUI(m["FounderID"].ToString())),
            AccessToken = string.Empty,
            IsMaturePublish = Convert.ToBoolean(m["MaturePublish"].ToString()),
            IsOpenEnrollment = Convert.ToBoolean(m["OpenEnrollment"].ToString()),
            MembershipFee = int.Parse(m["MembershipFee"].ToString()),
            IsShownInList = Convert.ToBoolean(m["OPenEnrollment"].ToString())
        };

        public static GroupRolemember ToGroupRolemember(this Map m, UGI group, AvatarNameServiceInterface avatarNameService) => new GroupRolemember
        {
            RoleID = m["RoleID"].AsUUID,
            Principal = avatarNameService.ResolveName(new UGUI(m["AgentID"].AsUUID)),
            Group = group
        };

        public static GroupRolemembership ToGroupRolemembership(this Map m, UGI group, AvatarNameServiceInterface avatarNameService) => new GroupRolemembership
        {
            RoleID = m["RoleID"].AsUUID,
            GroupTitle = m["Title"].ToString(),
            Group = group,
            Principal = avatarNameService.ResolveName(new UGUI(m["AgentID"].AsUUID))
        };

        public static GroupRolemembership ToGroupRolemembership(this Map m, AvatarNameServiceInterface avatarNameService) => new GroupRolemembership
        {
            RoleID = m["RoleID"].AsUUID,
            GroupTitle = m["Title"].ToString(),
            Group = new UGI(m["GroupID"].AsUUID),
            Principal = avatarNameService.ResolveName(new UGUI(m["AgentID"].AsUUID))
        };

        public static GroupRole ToGroupRole(this Map m, UGI group) => new GroupRole
        {
            Group = group,
            ID = m["RoleID"].AsUUID,
            Members = m["Members"].AsUInt,
            Name = m["Name"].ToString(),
            Description = m["Description"].ToString(),
            Title = m["Title"].ToString(),
            Powers = (GroupPowers)m["Powers"].AsULong
        };

        public static GroupNotice ToGroupNotice(this Map m) => new GroupNotice
        {
            Group = new UGI(m["GroupID"].AsUUID),
            ID = m["NoticeID"].AsUUID,
            Timestamp = Date.UnixTimeToDateTime(m["Timestamp"].AsULong),
            FromName = m["FromName"].ToString(),
            Subject = m["Subject"].ToString(),
            Message = m["Message"].ToString()
        };
#warning TODO: Implement BinaryBucket conversion
    }
}
