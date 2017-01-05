// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Groups;
using System.Collections.Generic;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Groups
{
    public static class GroupsV2ExtensionMethods
    {
        internal static string Boolean2String(bool v)
        {
            return v ? "true" : "false";
        }

        internal static bool String2Boolean(string v)
        {
            int i;
            if(v == "true")
            {
                return true;
            }
            else if(v == "false")
            {
                return false;
            }
            else if(int.TryParse(v, out i))
            {
                return i != 0;
            }
            else
            {
                return false;
            }
        }

        public static void ToXml(this GroupInfo info, XmlTextWriter writer, string tagname = "group")
        {
            writer.WriteStartElement(tagname);
            writer.WriteAttributeString("type", "List");
            writer.WriteNamedValue("AllowPublish", Boolean2String(info.IsAllowPublish));
            writer.WriteNamedValue("Charter", info.Charter);
            writer.WriteNamedValue("FounderID", info.Founder.ID);
            writer.WriteNamedValue("FounderUUI", info.Founder.ToString());
            writer.WriteNamedValue("GroupID", info.ID.ID);
            writer.WriteNamedValue("GroupName", info.ID.GroupName);
            writer.WriteNamedValue("InsigniaID", info.InsigniaID);
            writer.WriteNamedValue("MaturePublish", Boolean2String(info.IsMaturePublish));
            writer.WriteNamedValue("MembershipFee", info.MembershipFee);
            writer.WriteNamedValue("OpenEnrollment", Boolean2String(info.IsOpenEnrollment));
            writer.WriteNamedValue("OwnerRoleID", info.OwnerRoleID);
            writer.WriteNamedValue("ServiceLocation", null != info.ID.HomeURI ? info.ID.HomeURI.ToString() : string.Empty);
            writer.WriteNamedValue("ShownInList", Boolean2String(info.IsShownInList));
            writer.WriteNamedValue("MemberCount", info.MemberCount);
            writer.WriteNamedValue("RoleCount", info.RoleCount);
            writer.WriteEndElement();
        }

        public static void ToXml(this IEnumerable<GroupInfo> list, XmlTextWriter writer)
        {
            int index = 0;
            foreach(GroupInfo info in list)
            {
                info.ToXml(writer, "group" + index.ToString());
                ++index;
            }
        }

        public static void ToXml(this GroupRole role, XmlTextWriter writer, string tagname)
        {
            writer.WriteStartElement(tagname);
            writer.WriteAttributeString("type", "List");
            writer.WriteNamedValue("Description", role.Description);
            writer.WriteNamedValue("Members", role.Members);
            writer.WriteNamedValue("Name", role.Name);
            writer.WriteNamedValue("Powers", ((ulong)role.Powers).ToString());
            writer.WriteNamedValue("RoleID", role.ID);
            writer.WriteNamedValue("Title", role.Title);
            writer.WriteEndElement();
        }

        public static void ToXml(this List<GroupRole> roles, XmlTextWriter writer)
        {
            int index = 0;
            foreach(GroupRole role in roles)
            {
                role.ToXml(writer, "r-" + index.ToString());
                ++index;
            }
        }

        public static void ToXml(this GroupInvite invite, XmlTextWriter writer, string tagname)
        {
            writer.WriteStartElement(tagname);
            writer.WriteAttributeString("type", "List");
            writer.WriteNamedValue("InviteID", invite.ID);
            writer.WriteNamedValue("GroupID", invite.Group.ID);
            writer.WriteNamedValue("RoleID", invite.RoleID);
            writer.WriteNamedValue("AgentID", invite.Principal.ID);
            writer.WriteEndElement();
        }

        public static void ToXml(this GroupMembership membership, XmlTextWriter writer, string tagname)
        {
            writer.WriteStartElement(tagname);
            writer.WriteAttributeString("type", "List");
            writer.WriteNamedValue("AcceptNotices", Boolean2String(membership.IsAcceptNotices));
            writer.WriteNamedValue("AccessToken", string.Empty);
            writer.WriteNamedValue("Active", "True");
            writer.WriteNamedValue("ActiveRole", membership.ActiveRoleID);
            writer.WriteNamedValue("AllowPublish", Boolean2String(membership.IsAllowPublish));
            writer.WriteNamedValue("Charter", membership.Charter);
            writer.WriteNamedValue("Contribution", membership.Contribution);
            writer.WriteNamedValue("FounderID", membership.Founder.ID);
            writer.WriteNamedValue("GroupID", membership.Group.ID);
            writer.WriteNamedValue("GroupName", membership.Group.GroupName);
            writer.WriteNamedValue("GroupPicture", membership.GroupInsigniaID);
            writer.WriteNamedValue("GroupPowers", ((ulong)membership.GroupPowers).ToString());
            writer.WriteNamedValue("GroupTitle", membership.GroupTitle);
            writer.WriteNamedValue("ListInProfile", Boolean2String(membership.IsListInProfile));
            writer.WriteNamedValue("MaturePublish", Boolean2String(membership.IsMaturePublish));
            writer.WriteNamedValue("MembershipFee", membership.MembershipFee);
            writer.WriteNamedValue("OpenEnrollment", Boolean2String(membership.IsOpenEnrollment));
            writer.WriteNamedValue("ShowInList", Boolean2String(membership.IsShownInList));
            writer.WriteEndElement();
        }

        public static void ToXml(this List<GroupMembership> memberships, XmlTextWriter writer)
        {
            int index = 0;
            foreach(GroupMembership membership in memberships)
            {
                membership.ToXml(writer, "m-" + index.ToString());
                ++index;
            }
        }

        public static void ToXml(this GroupNotice notice, XmlTextWriter writer, string tagname)
        {
            writer.WriteStartElement(tagname);
            writer.WriteAttributeString("type", "List");
            writer.WriteNamedValue("GroupID", notice.Group.ID);
            writer.WriteNamedValue("NoticeID", notice.ID);
            writer.WriteNamedValue("Timestamp", notice.Timestamp.AsULong.ToString());
            writer.WriteNamedValue("FromName", notice.FromName);
            writer.WriteNamedValue("Subject", notice.Subject);
            writer.WriteNamedValue("Message", notice.Message);
            writer.WriteNamedValue("HasAttachment", Boolean2String(notice.HasAttachment));
            writer.WriteNamedValue("AttachmentItemID", notice.AttachmentItemID);
            writer.WriteNamedValue("AttachmentName", notice.AttachmentName);
            writer.WriteNamedValue("AttachmentType", (int)notice.AttachmentType);
            writer.WriteNamedValue("AttachmentOwnerID", notice.AttachmentOwner.ID);
            writer.WriteEndElement();
        }

        public static void ToXml(this List<GroupNotice> notices, XmlTextWriter writer)
        {
            int index = 0;
            foreach(GroupNotice notice in notices)
            {
                notice.ToXml(writer, "n-" + index.ToString());
                ++index;
            }
        }

        public static void ToXml(this GroupRolemember rolemember, XmlTextWriter writer, string tagname, bool excludePowers)
        {
            writer.WriteStartElement(tagname);
            writer.WriteAttributeString("type", "List");
            writer.WriteNamedValue("RoleID", rolemember.RoleID);
            writer.WriteNamedValue("MemberID", rolemember.Principal.ID);
            if (!excludePowers)
            {
                writer.WriteNamedValue("Powers", ((ulong)rolemember.Powers).ToString());
            }
            writer.WriteEndElement();
        }

        public static void ToXml(this List<GroupRolemember> rolemembers, XmlTextWriter writer, bool excludePowers)
        {
            int index = 0;
            foreach(GroupRolemember rolemember in rolemembers)
            {
                rolemember.ToXml(writer, "rm-" + index.ToString(), excludePowers);
                ++index;
            }
        }

        public static void ToXml(this GroupMember member, XmlTextWriter writer, string tagname, GroupPowers powers, bool isOwner, string groupTitle)
        {
            writer.WriteStartElement(tagname);
            writer.WriteAttributeString("type", "List");
            writer.WriteNamedValue("AcceptNotices", Boolean2String(member.IsAcceptNotices));
            writer.WriteNamedValue("AccessToken", member.AccessToken);
            writer.WriteNamedValue("AgentID", member.Principal.ID);
            writer.WriteNamedValue("AgentPowers", ((ulong)powers).ToString());
            writer.WriteNamedValue("Contribution", member.Contribution);
            writer.WriteNamedValue("IsOwner", isOwner);
            writer.WriteNamedValue("ListInProfile", Boolean2String(member.IsListInProfile));
            writer.WriteNamedValue("OnlineStatus", false);
            writer.WriteNamedValue("Title", groupTitle);
            writer.WriteEndElement();
        }
    }
}
