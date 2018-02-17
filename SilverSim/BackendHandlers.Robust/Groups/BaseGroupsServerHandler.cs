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

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Management.IM;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Groups;
using SilverSim.Types.StructuredData.REST;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Groups
{
    public abstract class BaseGroupsServerHandler : IPlugin
    {
        protected GroupsServiceInterface GroupsService { get; private set; }
        private readonly string m_GroupsServiceName;
        protected readonly Dictionary<string, Action<HttpRequest, Dictionary<string, object>>> MethodHandlers = new Dictionary<string, Action<HttpRequest, Dictionary<string, object>>>();
        protected BaseHttpServer HttpServer { get; private set; }
        protected IMRouter IMRouter { get; }
        protected abstract string UrlPath { get; }

        internal BaseGroupsServerHandler(IConfig ownSection)
        {
            m_GroupsServiceName = ownSection.GetString("GroupsService", "GroupsService");
        }

        public virtual void Startup(ConfigurationLoader loader)
        {
            MethodHandlers.Add("ADDNOTICE", HandleAddNotice);
            MethodHandlers.Add("GETGROUP", HandleGetGroup);
            MethodHandlers.Add("GETROLEMEMBERS", HandleGetRoleMembers);
            MethodHandlers.Add("GETGROUPROLES", HandleGetGroupRoles);
            MethodHandlers.Add("GETGROUPMEMBERS", HandleGetGroupMembers);
            GroupsService = loader.GetService<GroupsServiceInterface>(m_GroupsServiceName);
            HttpServer = loader.HttpServer;
            HttpServer.UriHandlers.Add(UrlPath, GroupsHandler);
            BaseHttpServer https;
            if(loader.TryGetHttpsServer(out https))
            {
                https.UriHandlers.Add(UrlPath, GroupsHandler);
            }
        }

        private void GroupsHandler(HttpRequest req)
        {
            if (req.ContainsHeader("X-SecondLife-Shard"))
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Request source not allowed");
                return;
            }

            if (req.Method != "POST")
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                return;
            }

            Dictionary<string, object> data;
            try
            {
                data = REST.ParseREST(req.Body);
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if (!data.ContainsKey("METHOD"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            string method = data["METHOD"].ToString();
            Action<HttpRequest, Dictionary<string, object>> handler;

            if(MethodHandlers.TryGetValue(method, out handler))
            {
                handler(req, data);
            }
            else
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
            }
        }

        protected static bool String2Boolean(string s)
        {
            int i;
            if(s.ToLower() == "true")
            {
                return true;
            }
            else if(s.ToLower() == "false")
            {
                return false;
            }
            else if(int.TryParse(s, out i))
            {
                return i != 0;
            }
            else
            {
                return false;
            }
        }

        protected virtual void HandleGetRoleMembers(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            UUID groupID;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            List<GroupRolemember> rolemembers = GroupsService.Rolemembers[requestingAgentID, new UGI(groupID)];
            if (rolemembers.Count != 0)
            {
                using (HttpResponse res = req.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteStartElement("RESULT");
                        writer.WriteAttributeString("type", "List");
                        rolemembers.ToXml(writer, true);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
            }
            else
            {
                SendNullResult(req, "No members");
            }
        }

        protected virtual void HandleGetGroup(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            UUID groupID = UUID.Zero;
            string groupName = string.Empty;
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                if(reqdata.ContainsKey("GroupID"))
                {
                    groupID = reqdata["GroupID"].ToString();
                }
                else if(reqdata.ContainsKey("Name"))
                {
                    groupName = reqdata["Name"].ToString();
                }
                else
                {
                    throw new InvalidDataException();
                }
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            GroupInfo group;
            if(groupID != UUID.Zero)
            {
                if(!GroupsService.Groups.TryGetValue(requestingAgent, new UGI(groupID), out group))
                {
                    SendNullResult(req, "No such group");
                    return;
                }
            }
            else
            {
                if (!GroupsService.Groups.TryGetValue(requestingAgent, groupName, out group))
                {
                    SendNullResult(req, "No such group");
                    return;
                }
            }
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    group.ToXml(writer, "RESULT");
                    writer.WriteEndElement();
                }
            }
        }

        protected virtual void HandleGetGroupRoles(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            UUID groupID;
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            List<GroupRole> roles = GroupsService.Roles[requestingAgent, new UGI(groupID)];
            if (roles.Count == 0)
            {
                SendNullResult(req, "No Roles found");
            }
            else
            {
                using (HttpResponse res = req.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteStartElement("RESULT");
                        writer.WriteAttributeString("type", "List");
                        roles.ToXml(writer);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
            }
        }

        private sealed class GroupMemberExt : GroupMember
        {
            public readonly GroupPowers Powers;
            public readonly string GroupTitle;

            public GroupMemberExt(GroupMember member, GroupsServiceInterface groupsService)
            {
                try
                {
                    Powers = groupsService.GetAgentPowers(member.Group, member.Principal);
                }
                catch
                {
                    Powers = GroupPowers.None;
                }
                GroupRole groupRole;
                try
                {
                    if (groupsService.Roles.TryGetValue(member.Principal, member.Group, member.SelectedRoleID, out groupRole))
                    {
                        GroupTitle = groupRole.Title;
                    }
                }
                catch
                {
                    GroupTitle = string.Empty;
                }
            }
        }

        protected virtual void HandleGetGroupMembers(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            UUID groupID;
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            GroupInfo groupInfo;
            if(!GroupsService.Groups.TryGetValue(requestingAgent, new UGI(groupID), out groupInfo))
            {
                SendNullResult(req, "Group not found");
                return;
            }

            var members = new List<GroupMemberExt>(from member in GroupsService.Members[requestingAgent, groupInfo.ID] select new GroupMemberExt(member, GroupsService));
            List<GroupRolemember> rolemembers = GroupsService.Rolemembers[requestingAgent, groupInfo.ID, groupInfo.OwnerRoleID];
            var owners = new List<UUI>(from rolemember in rolemembers select rolemember.Principal);

            if(members.Count != 0)
            {
                using (HttpResponse res = req.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteStartElement("RESULT");
                        writer.WriteAttributeString("type", "List");
                        int index = 0;
                        foreach(GroupMemberExt member in members)
                        {
                            member.ToXml(writer, "m-" + index.ToString(), member.Powers, owners.Contains(member.Principal), member.GroupTitle);
                            ++index;
                        }
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
            }
            else
            {
                SendNullResult(req, "No members");
            }
        }

        private void HandleAddNotice(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            var notice = new GroupNotice();
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                notice.Group.ID = reqdata["GroupID"].ToString();
                notice.ID = reqdata["NoticeID"].ToString();
                notice.FromName = reqdata["FromName"].ToString();
                notice.Subject = reqdata["Subject"].ToString();
                notice.Message = reqdata["Message"].ToString();
                notice.HasAttachment = String2Boolean(reqdata["HasAttachment"].ToString());
                if(notice.HasAttachment)
                {
                    notice.AttachmentType = (AssetType)int.Parse(reqdata["AttachmentType"].ToString());
                    notice.AttachmentName = reqdata["AttachmentName"].ToString();
                    notice.AttachmentItemID = reqdata["AttachmentItemID"].ToString();
                    notice.AttachmentOwner.ID = reqdata["AttachmentOwnerID"].ToString();
                }
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                GroupsService.Notices.Add(requestingAgent, notice);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        protected void SendBooleanResponse(HttpRequest req, bool result, string msg = "")
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteNamedValue("RESULT", result ? "true" : "false");
                    writer.WriteEndElement();
                }
            }
        }

        protected void SendNullResult(HttpRequest req, string msg)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteNamedValue("RESULT", "NULL");
                    writer.WriteNamedValue("REASON", msg);
                    writer.WriteEndElement();
                }
            }
        }
    }
}
