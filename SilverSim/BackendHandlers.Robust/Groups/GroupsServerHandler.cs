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
                default:
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    break;
            }
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
                default:
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    break;
            }
        }
        #endregion

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
