// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
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
        static readonly ILog m_Log = LogManager.GetLogger("ROBUST GROUPS HANDLER");
        public RobustGroupsServerHandler(IConfig ownSection)
            : base(ownSection)
        {
        }

        protected override string UrlPath
        {
            get
            {
                return "/groups";
            }
        }

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
        }

        void HandleAddAgentToGroup(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            UUI agentID;
            UUID groupID;
            UUID roleID;
            string accessToken;
            try
            {
                requestingAgentID = new UUI(reqdata["RequestAgentID"].ToString());
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
            if(!m_GroupsService.Groups.TryGetValue(requestingAgentID, new UGI(groupID), out groupInfo) ||
                !m_GroupsService.Roles.TryGetValue(requestingAgentID, new UGI(groupID), roleID, out groupRole))
            {
                SendNullResult(req, "Group and/or role not found");
                return;
            }
            GroupMember groupmember;
            try
            {
                groupmember = m_GroupsService.AddAgentToGroup(requestingAgentID, new UGI(groupID), roleID, agentID, accessToken);
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
                    writer.WriteStartElement("RESULT");
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
                    writer.WriteEndElement();
                }
            }
        }

        void HandleRemoveAgentFromGroup(HttpRequest req, Dictionary<string, object> reqdata)
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
                m_GroupsService.Members.Delete(requestingAgentID, new UGI(groupID), agentID);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        #region INVITE
        void HandleInvite(HttpRequest req, Dictionary<string, object> reqdata)
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

        void HandleInviteAdd(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgentID;
            GroupInvite invite = new GroupInvite();
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
                m_GroupsService.Invites.Add(requestingAgentID, invite);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        void HandleInviteGet(HttpRequest req, Dictionary<string, object> reqdata)
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
            if(m_GroupsService.Invites.TryGetValue(requestingAgentID, inviteID, out invite))
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

        void HandleInviteDelete(HttpRequest req, Dictionary<string, object> reqdata)
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
                m_GroupsService.Invites.Delete(requestingAgentID, inviteID);
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
        void HandleAgentRole(HttpRequest req, Dictionary<string, object> reqdata)
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
                default:
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    break;
            }
        }
        #endregion

        #region PUTROLE
        void HandlePutRole(HttpRequest req, Dictionary<string, object> reqdata)
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

        void HandlePutRoleAdd(HttpRequest req, Dictionary<string, object> reqdata)
        {
            GroupRole role = new GroupRole();
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
                m_GroupsService.Roles.Add(requestingAgentID, role);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        void HandlePutRoleUpdate(HttpRequest req, Dictionary<string, object> reqdata)
        {
            GroupRole role = new GroupRole();
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

            if (!m_GroupsService.Roles.TryGetValue(requestingAgentID, role.Group, role.ID, out role))
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
                m_GroupsService.Roles.Update(requestingAgentID, role);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }
        #endregion

        void HandleRemoveRole(HttpRequest req, Dictionary<string, object> reqdata)
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
                m_GroupsService.Roles.Delete(requestingAgent, new UGI(groupID), roleID);
            }
            catch
            {
                SendBooleanResponse(req, false);
                return;
            }
            SendBooleanResponse(req, true);
        }

        #region PUTGROUP
        void HandlePutGroup(HttpRequest req, Dictionary<string, object> reqdata)
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

                default:
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    break;
            }
        }

        void HandlePutGroupAdd(HttpRequest req, Dictionary<string, object> reqdata)
        {
            GroupInfo gInfo = new GroupInfo();
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
                gInfo = m_GroupsService.CreateGroup(requestingAgentID, gInfo, GroupPowers.DefaultEveryonePowers, GroupPowers.OwnerPowers);
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
        #endregion
    }
    #endregion

    #region Factory
    [PluginName("GroupsHandler")]
    public sealed class RobustGroupsServerHandlerFactory : IPluginFactory
    {
        public RobustGroupsServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustGroupsServerHandler(ownSection);
        }
    }
    #endregion
}
