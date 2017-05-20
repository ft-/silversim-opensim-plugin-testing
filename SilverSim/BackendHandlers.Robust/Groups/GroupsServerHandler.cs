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

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Groups
{
    #region Service Implementation
    [Description("Robust Groups Protocol Server")]
    public sealed class RobustGroupsServerHandler : BaseGroupsServerHandler
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GROUPS HANDLER");
        public RobustGroupsServerHandler(IConfig ownSection)
            : base(ownSection)
        {
        }

        protected override string UrlPath => "/groups";

        public override void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for Groups server");
            base.Startup(loader);
            MethodHandlers.Add("PUTGROUP", HandlePutGroup);
            MethodHandlers.Add("PUTROLE", HandlePutRole);
            MethodHandlers.Add("AGENTROLE", HandleAgentRole);
            MethodHandlers.Add("INVITE", HandleInvite);
            MethodHandlers.Add("REMOVEROLE", HandleRemoveRole);
            MethodHandlers.Add("ADDAGENTTOGROUP", HandleAddAgentToGroup);
            MethodHandlers.Add("REMOVEAGENTFROMGROUP", HandleRemoveAgentFromGroup);
            MethodHandlers.Add("GETNOTICES", HandleGetNotices);
            MethodHandlers.Add("GETMEMBERSHIP", HandleGetMembership);
            MethodHandlers.Add("SETACTIVE", HandleSetActive);
            MethodHandlers.Add("GETAGENTROLES", HandleGetAgentRoles);
            MethodHandlers.Add("UPDATEMEMBERSHIP", HandleUpdateMembership);
            MethodHandlers.Add("FINDGROUPS", HandleFindGroups);
        }

        private void WriteGroupMembershipData(XmlTextWriter writer, string tagname, GroupInfo groupInfo, GroupMember groupmember, GroupRole groupRole)
        {
            writer.WriteStartElement(tagname);
            writer.WriteAttributeString("type", "List");
            writer.WriteNamedValue("AcceptNotices", GroupsV2ExtensionMethods.Boolean2String(groupmember.IsAcceptNotices));
            writer.WriteNamedValue("AccessToken", string.Empty);
            writer.WriteNamedValue("Active", "True");
            writer.WriteNamedValue("ActiveRole", groupmember.SelectedRoleID);
            writer.WriteNamedValue("AllowPublish", GroupsV2ExtensionMethods.Boolean2String(groupInfo.IsAllowPublish));
            writer.WriteNamedValue("Charter", groupInfo.Charter);
            writer.WriteNamedValue("Contribution", groupmember.Contribution);
            writer.WriteNamedValue("FounderID", groupInfo.Founder.ID);
            writer.WriteNamedValue("GroupID", groupInfo.ID.ID);
            writer.WriteNamedValue("GroupName", groupInfo.ID.GroupName);
            writer.WriteNamedValue("GroupPicture", groupInfo.InsigniaID);
            writer.WriteNamedValue("GroupPowers", ((ulong)groupRole.Powers).ToString());
            writer.WriteNamedValue("GroupTitle", groupRole.Title);
            writer.WriteNamedValue("ListInProfile", GroupsV2ExtensionMethods.Boolean2String(groupmember.IsListInProfile));
            writer.WriteNamedValue("MaturePublish", GroupsV2ExtensionMethods.Boolean2String(groupInfo.IsMaturePublish));
            writer.WriteNamedValue("MembershipFee", groupInfo.MembershipFee);
            writer.WriteNamedValue("OpenEnrollment", GroupsV2ExtensionMethods.Boolean2String(groupInfo.IsOpenEnrollment));
            writer.WriteNamedValue("ShowInList", GroupsV2ExtensionMethods.Boolean2String(groupInfo.IsShownInList));
            writer.WriteEndElement();
        }

        private void HandleFindGroups(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            List<DirGroupInfo> groups;
            string query = string.Empty;

            if (reqdata.ContainsKey("Query"))
            {
                query = reqdata["Query"].ToString();
            }
            groups = GroupsService.Groups.GetGroupsByName(requestingAgent, query);

            if(groups.Count != 0)
            {
                using (HttpResponse res = req.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        int index = 0;
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteStartElement("RESULT");
                        writer.WriteAttributeString("type", "List");
                        foreach(DirGroupInfo groupInfo in groups)
                        {
                            writer.WriteStartElement("n-" + index.ToString());
                            writer.WriteAttributeString("type", "List");
                            writer.WriteNamedValue("GroupID", groupInfo.ID.ID);
                            writer.WriteNamedValue("Name", groupInfo.ID.GroupName);
                            writer.WriteNamedValue("NMembers", groupInfo.MemberCount);
                            writer.WriteNamedValue("SearchOrder", groupInfo.SearchOrder);
                            writer.WriteEndElement();
                            ++index;

                            if(index == 100)
                            {
                                break;
                            }
                        }
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
            }
            else
            {
                SendNullResult(req, "No groups found");
            }
        }

        private void HandleAddAgentToGroup(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            UUI agentID;
            UUID groupID;
            UUID roleID;
            string accessToken;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                agentID = new UUI(reqdata["AgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
                roleID = reqdata["RoleID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if(reqdata.ContainsKey("AccessToken"))
            {
                accessToken = reqdata["AccessToken"].ToString();
            }
            else
            {
                accessToken = UUID.Random.ToString();
            }

            GroupInfo groupInfo;
            GroupRole groupRole;
            if(!GroupsService.Groups.TryGetValue(requestingAgentID, new UGI(groupID), out groupInfo) ||
                !GroupsService.Roles.TryGetValue(requestingAgentID, new UGI(groupID), roleID, out groupRole))
            {
                SendNullResult(req, "Group and/or role not found");
                return;
            }
            GroupMember groupmember;
            try
            {
                groupmember = GroupsService.AddAgentToGroup(requestingAgentID, new UGI(groupID), roleID, agentID, accessToken);
            }
            catch
            {
                SendNullResult(req, "Failed to add member");
                return;
            }
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    WriteGroupMembershipData(writer, "RESULT", groupInfo, groupmember, groupRole);
                    writer.WriteEndElement();
                }
            }
        }

        private void HandleGetMembership(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            UUI agentID;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                agentID = new UUI(reqdata["AgentID"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if(reqdata.ContainsKey("ALL"))
            {
                List<GroupMembership> memberships;
                try
                {
                    memberships = GroupsService.Memberships[requestingAgentID, agentID];
                }
                catch
                {
                    memberships = new List<GroupMembership>();
                }

                if (memberships.Count == 0)
                {
                    SendNullResult(req, "No memberships");
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
                            memberships.ToXml(writer);
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                    }
                }
            }
            else
            {
                UGI group;
                if (reqdata.ContainsKey("GroupID"))
                {
                    group = UGI.Unknown;
                    try
                    {
                        group.ID = reqdata["GroupID"].ToString();
                    }
                    catch
                    {
                        req.ErrorResponse(HttpStatusCode.BadRequest);
                        return;
                    }
                }
                else
                {
                    if(!GroupsService.ActiveGroup.TryGetValue(requestingAgentID, agentID, out group) || group.ID == UUID.Zero)
                    {
                        SendNullResult(req, "No active group");
                        return;
                    }
                }

                GroupMembership memship;
                if(GroupsService.Memberships.TryGetValue(requestingAgentID, group, agentID, out memship))
                {
                    using (HttpResponse res = req.BeginResponse("text/xml"))
                    {
                        using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                        {
                            writer.WriteStartElement("ServerResponse");
                            memship.ToXml(writer, "RESULT");
                            writer.WriteEndElement();
                        }
                    }
                }
                else
                {
                    SendNullResult(req, "Group not found");
                }
            }
        }

        private void HandleUpdateMembership(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            UUI agent;
            UUID groupID;

            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                agent = new UUI(reqdata["AgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            GroupMember member;
            if(!GroupsService.Members.TryGetValue(requestingAgent, new UGI(groupID), agent, out member))
            {
                SendBooleanResponse(req, false);
                return;
            }

            try
            {
                member.IsListInProfile = GroupsV2ExtensionMethods.String2Boolean(reqdata["ListInProfile"].ToString());
                member.IsAcceptNotices = GroupsV2ExtensionMethods.String2Boolean(reqdata["AcceptNotices"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                GroupsService.Members.Update(requestingAgent, member.Group, member.Principal, member.IsAcceptNotices, member.IsListInProfile);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        private void HandleRemoveAgentFromGroup(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            UUI agentID;
            UUID groupID;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                agentID = new UUI(reqdata["AgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                GroupsService.Members.Delete(requestingAgentID, new UGI(groupID), agentID);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        #region SETACTIVE
        private void HandleSetActive(HttpRequest req, Dictionary<string, object> reqdata)
        {
            string op;
            try
            {
                op = reqdata["OP"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            switch (op)
            {
                case "GROUP":
                    HandleSetActiveGroup(req, reqdata);
                    break;

                case "ROLE":
                    HandleSetActiveRole(req, reqdata);
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    break;
            }
        }

        private void HandleSetActiveGroup(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            UUI agentID;
            UUID groupID;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                agentID = new UUI(reqdata["AgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if(UUID.Zero == groupID)
            {
                GroupsService.ActiveGroup[requestingAgentID, agentID] = new UGI(groupID);
                SendNullResult(req, "No active group");
            }
            else
            {
                GroupInfo groupInfo;
                GroupMember member;
                GroupRole role;
                try
                {
                    groupInfo = GroupsService.Groups[requestingAgentID, new UGI(groupID)];
                    member = GroupsService.Members[requestingAgentID, groupInfo.ID, agentID];
                    role = GroupsService.Roles[requestingAgentID, groupInfo.ID, member.SelectedRoleID];
                }
                catch
                {
                    SendNullResult(req, "Not a member");
                    return;
                }

                GroupsService.ActiveGroup[requestingAgentID, agentID] = groupInfo.ID;
                using (HttpResponse res = req.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        WriteGroupMembershipData(writer, "RESULT", groupInfo, member, role);
                        writer.WriteEndElement();
                    }
                }
            }
        }

        private void HandleSetActiveRole(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            UUI agentID;
            UUID groupID;
            UUID roleID;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                agentID = new UUI(reqdata["AgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
                roleID = reqdata["RoleID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                if (GroupsService.Rolemembers.ContainsKey(requestingAgentID, new UGI(groupID), roleID, agentID))
                {
                    GroupsService.ActiveGroup[requestingAgentID, new UGI(groupID), agentID] = roleID;
                }
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }
        #endregion

        #region INVITE
        private void HandleInvite(HttpRequest req, Dictionary<string, object> reqdata)
        {
            string op;
            try
            {
                op = reqdata["OP"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            switch (op)
            {
                case "ADD":
                    HandleInviteAdd(req, reqdata);
                    break;

                case "DELETE":
                    HandleInviteDelete(req, reqdata);
                    break;

                case "GET":
                    HandleInviteGet(req, reqdata);
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    break;
            }
        }

        private void HandleInviteAdd(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            var invite = new GroupInvite();
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                invite.ID = reqdata["InviteID"].ToString();
                invite.Group.ID = reqdata["GroupID"].ToString();
                invite.RoleID = reqdata["RoleID"].ToString();
                invite.Principal = new UUI(reqdata["AgentID"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                GroupsService.Invites.Add(requestingAgentID, invite);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        private void HandleInviteGet(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            UUID inviteID;

            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                inviteID = reqdata["InviteID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            GroupInvite invite;
            if(GroupsService.Invites.TryGetValue(requestingAgentID, inviteID, out invite))
            {
                using (HttpResponse res = req.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        invite.ToXml(writer, "RESULT");
                        writer.WriteEndElement();
                    }
                }
            }
            else
            {
                SendNullResult(req, "Not found");
            }
        }

        private void HandleInviteDelete(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            UUID inviteID;

            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                inviteID = reqdata["InviteID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                GroupsService.Invites.Delete(requestingAgentID, inviteID);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }
        #endregion

        #region AGENTROLE
        private void HandleAgentRole(HttpRequest req, Dictionary<string, object> reqdata)
        {
            string op;
            try
            {
                op = reqdata["OP"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            switch (op)
            {
                case "ADD":
                    HandleAgentRoleAdd(req, reqdata);
                    break;

                case "DELETE":
                    HandleAgentRoleDelete(req, reqdata);
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    break;
            }
        }

        private void HandleAgentRoleAdd(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            var rolemember = new GroupRolemember();

            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                rolemember.Group.ID = reqdata["GroupID"].ToString();
                rolemember.Principal = new UUI(reqdata["AgentID"].ToString());
                rolemember.RoleID = reqdata["RoleID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if(GroupsService.Members.ContainsKey(requestingAgent, rolemember.Group, rolemember.Principal))
            {
                try
                {
                    GroupsService.Rolemembers.Add(requestingAgent, rolemember);
                }
                catch
                {
                    SendBooleanResponse(req, false);
                    return;
                }
                SendBooleanResponse(req, true);
            }
            SendBooleanResponse(req, false);
        }

        private void HandleAgentRoleDelete(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            UUID groupID;
            UUID roleID;
            UUI agent;
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                agent = new UUI(reqdata["AgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
                roleID = reqdata["RoleID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            if(UUID.Zero == roleID)
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                GroupsService.Rolemembers.Delete(requestingAgent, new UGI(groupID), roleID, agent);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }
        #endregion

        #region PUTROLE
        private void HandlePutRole(HttpRequest req, Dictionary<string, object> reqdata)
        {
            string op;
            try
            {
                op = reqdata["OP"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            switch (op)
            {
                case "ADD":
                    HandlePutRoleAdd(req, reqdata);
                    break;

                case "UPDATE":
                    HandlePutRoleUpdate(req, reqdata);
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    break;
            }
        }

        private void HandlePutRoleAdd(HttpRequest req, Dictionary<string, object> reqdata)
        {
            var role = new GroupRole();
            UUI requestingAgentID;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                role.ID = reqdata["RoleID"].ToString();
                role.Group.ID = reqdata["GroupID"].ToString();
                role.Name = reqdata["Name"].ToString();
                role.Description = reqdata["Description"].ToString();
                role.Title = reqdata["Title"].ToString();
                role.Powers = (GroupPowers)ulong.Parse(reqdata["Powers"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                GroupsService.Roles.Add(requestingAgentID, role);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        private void HandlePutRoleUpdate(HttpRequest req, Dictionary<string, object> reqdata)
        {
            var role = new GroupRole();
            UUI requestingAgentID;

            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                role.ID = reqdata["RoleID"].ToString();
                role.Group.ID = reqdata["GroupID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if (!GroupsService.Roles.TryGetValue(requestingAgentID, role.Group, role.ID, out role))
            {
                SendBooleanResponse(req, false, "Role not found");
                return;
            }

            try
            {
                role.Name = reqdata["Name"].ToString();
                role.Description = reqdata["Description"].ToString();
                role.Title = reqdata["Title"].ToString();
                role.Powers = (GroupPowers)ulong.Parse(reqdata["Powers"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                GroupsService.Roles.Update(requestingAgentID, role);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }
        #endregion

        private void HandleGetAgentRoles(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            UUID groupID;
            UUI agentID;
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
                agentID = new UUI(reqdata["AgentID"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            GroupMember member;
            if(GroupsService.Members.TryGetValue(requestingAgent, new UGI(groupID), agentID, out member))
            {
                List<GroupRole> roles = GroupsService.Roles[requestingAgent, member.Group, member.Principal];
                if(roles.Count != 0)
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
                else
                {
                    SendNullResult(req, "No members");
                }
            }
            else
            {
                SendNullResult(req, "No members");
            }
        }

        private void HandleRemoveRole(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent;
            UUID groupID;
            UUID roleID;

            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                groupID = reqdata["GroupID"].ToString();
                roleID = reqdata["RoleID"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                GroupsService.Roles.Delete(requestingAgent, new UGI(groupID), roleID);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        #region PUTGROUP
        private void HandlePutGroup(HttpRequest req, Dictionary<string, object> reqdata)
        {
            string op;
            try
            {
                op = reqdata["OP"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            switch(op)
            {
                case "ADD":
                    HandlePutGroupAdd(req, reqdata);
                    break;

                case "UPDATE":
                    HandlePutGroupUpdate(req, reqdata);
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    break;
            }
        }

        private void HandlePutGroupAdd(HttpRequest req, Dictionary<string, object> reqdata)
        {
            var gInfo = new GroupInfo();
            gInfo.ID.ID = UUID.Random;
            UUI requestingAgentID;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
                gInfo.ID.GroupName = reqdata["Name"].ToString();
                if(reqdata.ContainsKey("Charter"))
                {
                    gInfo.Charter = reqdata["Charter"].ToString();
                }
                if(reqdata.ContainsKey("ShownInList"))
                {
                    gInfo.IsShownInList = GroupsV2ExtensionMethods.String2Boolean(reqdata["ShownInList"].ToString());
                }
                if(reqdata.ContainsKey("InsigniaID"))
                {
                    gInfo.InsigniaID = reqdata["InsigniaID"].ToString();
                }
                if(reqdata.ContainsKey("MembershipFee"))
                {
                    gInfo.MembershipFee = int.Parse(reqdata["MembershipFee"].ToString());
                }
                if(reqdata.ContainsKey("OpenEnrollment"))
                {
                    gInfo.IsOpenEnrollment = GroupsV2ExtensionMethods.String2Boolean(reqdata["OpenEnrollment"].ToString());
                }
                if (reqdata.ContainsKey("AllowPublish"))
                {
                    gInfo.IsAllowPublish = GroupsV2ExtensionMethods.String2Boolean(reqdata["AllowPublish"].ToString());
                }
                if (reqdata.ContainsKey("MaturePublish"))
                {
                    gInfo.IsMaturePublish = GroupsV2ExtensionMethods.String2Boolean(reqdata["MaturePublish"].ToString());
                }
                if (reqdata.ContainsKey("FounderID"))
                {
                    gInfo.Founder.ID = reqdata["FounderID"].ToString();
                }
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                gInfo = GroupsService.CreateGroup(requestingAgentID, gInfo, GroupPowers.DefaultEveryonePowers, GroupPowers.OwnerPowers);
            }
            catch
            {
                SendNullResult(req, "Group not created");
                return;
            }
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    gInfo.ToXml(writer, "RESULT");
                    writer.WriteEndElement();
                }
            }
        }

        private void HandlePutGroupUpdate(HttpRequest req, Dictionary<string, object> reqdata)
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

            GroupInfo groupInfo;
            if(!GroupsService.Groups.TryGetValue(requestingAgentID, new UGI(groupID), out groupInfo))
            {
                SendNullResult(req, string.Empty);
                return;
            }

            try
            {
                if (reqdata.ContainsKey("Charter"))
                {
                    groupInfo.Charter = reqdata["Charter"].ToString();
                }
                if (reqdata.ContainsKey("ShownInList"))
                {
                    groupInfo.IsShownInList = GroupsV2ExtensionMethods.String2Boolean(reqdata["ShownInList"].ToString());
                }
                if (reqdata.ContainsKey("InsigniaID"))
                {
                    groupInfo.InsigniaID = reqdata["InsigniaID"].ToString();
                }
                if(reqdata.ContainsKey("MembershipFee"))
                {
                    groupInfo.MembershipFee = int.Parse(reqdata["MembershipFee"].ToString());
                }
                if(reqdata.ContainsKey("OpenEnrollment"))
                {
                    groupInfo.IsOpenEnrollment = GroupsV2ExtensionMethods.String2Boolean(reqdata["OpenEnrollment"].ToString());
                }
                if(reqdata.ContainsKey("AllowPublish"))
                {
                    groupInfo.IsAllowPublish = GroupsV2ExtensionMethods.String2Boolean(reqdata["AllowPublish"].ToString());
                }
                if(reqdata.ContainsKey("MaturePublish"))
                {
                    groupInfo.IsMaturePublish = GroupsV2ExtensionMethods.String2Boolean(reqdata["MaturePublish"].ToString());
                }
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                groupInfo = GroupsService.Groups.Update(requestingAgentID, groupInfo);
            }
            catch
            {
                SendNullResult(req, string.Empty);
                return;
            }

            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    groupInfo.ToXml(writer, "RESULT");
                    writer.WriteEndElement();
                }
            }
        }
        #endregion

        private void HandleGetNotices(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestingAgentID"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if(reqdata.ContainsKey("NoticeID"))
            {
                UUID noticeID;
                try
                {
                    noticeID = reqdata["NoticeID"].ToString();
                }
                catch
                {
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    return;
                }

                GroupNotice notice;
                if (GroupsService.Notices.TryGetValue(requestingAgentID, noticeID, out notice))
                {
                    using (HttpResponse res = req.BeginResponse("text/xml"))
                    {
                        using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                        {
                            writer.WriteStartElement("ServerResponse");
                            notice.ToXml(writer, "RESULT");
                            writer.WriteEndElement();
                        }
                    }
                }
                else
                {
                    SendNullResult(req, "Not found");
                }
            }
            else if(reqdata.ContainsKey("GroupID"))
            {
                UUID groupID;
                try
                {
                    groupID = reqdata["GroupID"].ToString();
                }
                catch
                {
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    return;
                }

                List<GroupNotice> notices = GroupsService.Notices.GetNotices(requestingAgentID, new UGI(groupID));
                if (notices.Count != 0)
                {
                    using (HttpResponse res = req.BeginResponse("text/xml"))
                    {
                        using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                        {
                            writer.WriteStartElement("ServerResponse");
                            writer.WriteStartElement("RESULT");
                            writer.WriteAttributeString("type", "List");
                            notices.ToXml(writer);
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                    }
                }
                else
                {
                    SendNullResult(req, "No group notices");
                }
            }
            else
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("GroupsHandler")]
    public sealed class RobustGroupsServerHandlerFactory : IPluginFactory
    {
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection) =>
            new RobustGroupsServerHandler(ownSection);
    }
    #endregion
}
