// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces;
using SilverSim.Types;
using SilverSim.Types.Groups;
using SilverSim.Types.IM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;

namespace SilverSim.BackendHandlers.Robust.Groups
{
    [Description("Robust HG-Groups Protocol Server")]
    public class RobustHGGroupsServerHandler : BaseGroupsServerHandler, IServiceURLsGetInterface
    {
        static readonly ILog m_Log = LogManager.GetLogger("ROBUST HGGROUPS HANDLER");
        public RobustHGGroupsServerHandler(IConfig ownSection)
            : base(ownSection)
        {
            MethodHandlers.Add("POSTGROUP", HandlePostGroup);
        }

        protected override string UrlPath
        {
            get
            {
                return "/hg-groups";
            }
        }

        public void GetServiceURLs(Dictionary<string, string> dict)
        {
            dict["GroupsServerURI"] = m_HttpServer.ServerURI;
        }

        public override void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for HG-Groups server");
            base.Startup(loader);
        }

        void HandlePostGroup(HttpRequest req, Dictionary<string, object> reqdata)
        {
            GroupInfo grec_new = new GroupInfo();
            GroupInvite ginvite = new GroupInvite();
            UUI agent;
            UUI requestingAgent;
            string accessToken;
            try
            {
                grec_new.ID.ID = reqdata["GroupID"].ToString();
                grec_new.ID.GroupName = reqdata["Name"].ToString();
                grec_new.ID.HomeURI = new Uri(reqdata["Location"].ToString(), UriKind.Absolute);
                accessToken = reqdata["AccessToken"].ToString();
                agent = new UUI(reqdata["AgentID"].ToString());
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            GroupInfo grec;
            if(!m_GroupsService.Groups.TryGetValue(requestingAgent, grec_new.ID, out grec))
            {
                try
                {
                    m_GroupsService.Groups.Update(requestingAgent, grec_new);
                }
                catch(Exception e)
                {
                    SendBooleanResponse(req, false, e.Message);
                    return;
                }
                grec = grec_new;
            }

            if(grec.ID.HomeURI == null || grec.ID.HomeURI.ToString() != grec_new.ID.HomeURI.ToString())
            {
                SendBooleanResponse(req, false, "Cannot create proxy membership for a non-proxy group");
                return;
            }

            ginvite.ID = UUID.Random;
            ginvite.Group = grec.ID;
            ginvite.RoleID = UUID.Zero;
            ginvite.Principal = agent;
            try
            {
                m_GroupsService.Invites.Add(requestingAgent, ginvite);
            }
            catch(Exception e)
            {
                SendBooleanResponse(req, false, e.Message);
                return;
            }

            GridInstantMessage gim = new GridInstantMessage();
            gim.FromGroup = grec.ID;
            gim.ToAgent = agent;
            gim.FromAgent = requestingAgent;
            gim.Dialog = GridInstantMessageDialog.GroupInvitation;
            gim.IsFromGroup = true;
            gim.Message = "Please confirm your acceptance to join group " + grec.ID.FullName;

            if(!m_IMRouter.SendSync(gim))
            {
                SendBooleanResponse(req, false);
                return;
            }

            
            try
            {
                m_GroupsService.Members.Add(requestingAgent, grec.ID, agent, UUID.Zero, accessToken);
            }
            catch(Exception e)
            {
                SendBooleanResponse(req, false, e.Message);
                return;
            }

            SendBooleanResponse(req, true);
        }

        bool VerifyAccessToken(UUI requestingAgent, UGI group, string accessToken)
        {
            GroupMember gmem;
            try
            {
                if(!m_GroupsService.Members.TryGetValue(requestingAgent, group, requestingAgent, out gmem))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            return gmem.AccessToken == accessToken;
        }

        protected override void HandleGetRoleMembers(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent = UUI.Unknown;
            UGI group = UGI.Unknown;
            string accessToken = string.Empty;
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                group.ID = reqdata["GroupID"].ToString();
                accessToken = reqdata["AccessToken"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            if(!VerifyAccessToken(requestingAgent, group, accessToken))
            {
                SendNullResult(req, string.Empty);
                return;
            }

            base.HandleGetRoleMembers(req, reqdata);
        }

        protected override void HandleGetGroupRoles(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent = UUI.Unknown;
            UGI group = UGI.Unknown;
            string accessToken = string.Empty;
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                group.ID = reqdata["GroupID"].ToString();
                accessToken = reqdata["AccessToken"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            if (!VerifyAccessToken(requestingAgent, group, accessToken))
            {
                SendNullResult(req, string.Empty);
                return;
            }

            base.HandleGetGroupRoles(req, reqdata);
        }

        protected override void HandleGetGroupMembers(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUI requestingAgent = UUI.Unknown;
            UGI group = UGI.Unknown;
            string accessToken = string.Empty;
            try
            {
                requestingAgent = new UUI(reqdata["RequestingAgentID"].ToString());
                group.ID = reqdata["GroupID"].ToString();
                accessToken = reqdata["AccessToken"].ToString();
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }
            if (!VerifyAccessToken(requestingAgent, group, accessToken))
            {
                SendNullResult(req, string.Empty);
                return;
            }

            base.HandleGetGroupMembers(req, reqdata);
        }
    }

    #region Factory
    [PluginName("HGGroupsHandler")]
    public sealed class RobustHGGroupsServerHandlerFactory : IPluginFactory
    {
        public RobustHGGroupsServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustHGGroupsServerHandler(ownSection);
        }
    }
    #endregion
}
