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

        public static void ToXml(this GroupInfo info, XmlTextWriter writer, string tagname = "group", bool writeTypeAttr = true)
        {
            writer.WriteStartElement(tagname);
            if(writeTypeAttr)
            {
                writer.WriteAttributeString("type", "List");
            }
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
                info.ToXml(writer, "group" + index.ToString(), true);
                ++index;
            }
        }

        public static void ToXml(this GroupRole role, XmlTextWriter writer, string tagname, bool writeTypeAttr = true)
        {
            writer.WriteStartElement(tagname);
            if (writeTypeAttr)
            {
                writer.WriteAttributeString("type", "List");
            }
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
    }
}
