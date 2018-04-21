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
    [PluginName("HGGroupsHandler")]
    public class RobustHGGroupsServerHandler : BaseGroupsServerHandler, IServiceURLsGetInterface
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST HGGROUPS HANDLER");
        public RobustHGGroupsServerHandler(IConfig ownSection)
            : base(ownSection)
        {
            MethodHandlers.Add("POSTGROUP", HandlePostGroup);
        }

        protected override string UrlPath => "/hg-groups";

        public void GetServiceURLs(Dictionary<string, string> dict)
        {
            dict["GroupsServerURI"] = HttpServer.ServerURI;
        }

        public override void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for HG-Groups server");
            base.Startup(loader);
        }

        private void HandlePostGroup(HttpRequest req, Dictionary<string, object> reqdata)
        {
            var grec_new = new GroupInfo();
            var ginvite = new GroupInvite();
            UGUI agent;
            UGUIWithName requestingAgent;
            string accessToken;
            try
            {
                grec_new.ID.ID = reqdata["GroupID"].ToString();
                grec_new.ID.GroupName = reqdata["Name"].ToString();
                grec_new.ID.HomeURI = new Uri(reqdata["Location"].ToString(), UriKind.Absolute);
                accessToken = reqdata["AccessToken"].ToString();
                agent = new UGUI(reqdata["AgentID"].ToString());
                requestingAgent = new UGUIWithName(reqdata["RequestingAgentID"].ToString());
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            GroupInfo grec;
            if(!GroupsService.Groups.TryGetValue(requestingAgent, grec_new.ID, out grec))
            {
                try
                {
                    GroupsService.Groups.Update(requestingAgent, grec_new);
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
                GroupsService.Invites.Add(requestingAgent, ginvite);
            }
            catch(Exception e)
            {
                SendBooleanResponse(req, false, e.Message);
                return;
            }

            var gim = new GridInstantMessage
            {
                FromGroup = grec.ID,
                ToAgent = agent,
                FromAgent = requestingAgent,
                Dialog = GridInstantMessageDialog.GroupInvitation,
                IsFromGroup = true,
                Message = "Please confirm your acceptance to join group " + grec.ID.FullName
            };
            if (!IMRouter.SendSync(gim))
            {
                SendBooleanResponse(req, false);
                return;
            }

            try
            {
                GroupsService.Members.Add(requestingAgent, grec.ID, agent, UUID.Zero, accessToken);
            }
            catch(Exception e)
            {
                SendBooleanResponse(req, false, e.Message);
                return;
            }

            SendBooleanResponse(req, true);
        }

        private bool VerifyAccessToken(UGUI requestingAgent, UGI group, string accessToken)
        {
            GroupMember gmem;
            try
            {
                if(!GroupsService.Members.TryGetValue(requestingAgent, group, requestingAgent, out gmem))
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
            UGUI requestingAgent = UGUI.Unknown;
            UGI group = UGI.Unknown;
            string accessToken = string.Empty;
            try
            {
                requestingAgent = new UGUI(reqdata["RequestingAgentID"].ToString());
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
            UGUI requestingAgent = UGUI.Unknown;
            UGI group = UGI.Unknown;
            string accessToken = string.Empty;
            try
            {
                requestingAgent = new UGUI(reqdata["RequestingAgentID"].ToString());
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
            UGUI requestingAgent = UGUI.Unknown;
            UGI group = UGI.Unknown;
            string accessToken = string.Empty;
            try
            {
                requestingAgent = new UGUI(reqdata["RequestingAgentID"].ToString());
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
}
