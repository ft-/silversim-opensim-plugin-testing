// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Groups
{
    public abstract class BaseGroupsServerHandler : IPlugin
    {
        protected GroupsServiceInterface m_GroupsService { get; private set; }
        readonly string m_GroupsServiceName;
        protected readonly Dictionary<string, Action<HttpRequest, Dictionary<string, object>>> MethodHandlers = new Dictionary<string, Action<HttpRequest, Dictionary<string, object>>>();
        protected BaseHttpServer m_HttpServer { get; private set; }
        protected IMRouter m_IMRouter { get; private set; }
        protected abstract string UrlPath { get; }

        internal BaseGroupsServerHandler(IConfig ownSection)
        {
            m_GroupsServiceName = ownSection.GetString("GroupsService", "GroupsService");
        }

        public virtual void Startup(ConfigurationLoader loader)
        {
            MethodHandlers.Add("ADDNOTICE", HandleAddNotice);
            MethodHandlers.Add("GETROLEMEMBERS", HandleGetRoleMembers);
            MethodHandlers.Add("GETROLES", HandleGetRoles);
            MethodHandlers.Add("GETMEMBERS", HandleGetMembers);
            m_GroupsService = loader.GetService<GroupsServiceInterface>(m_GroupsServiceName);
            m_HttpServer = loader.HttpServer;
            m_HttpServer.UriHandlers.Add(UrlPath, GroupsHandler);
            try
            {
                loader.HttpsServer.UriHandlers.Add(UrlPath, GroupsHandler);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        void GroupsHandler(HttpRequest req)
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

        }

        protected virtual void HandleGetRoles(HttpRequest req, Dictionary<string, object> reqdata)
        {

        }

        protected virtual void HandleGetMembers(HttpRequest req, Dictionary<string, object> reqdata)
        {

        }

        void HandleAddNotice(HttpRequest req, Dictionary<string, object> reqdata)
        {
            /*
            RequestingAgentID =
            GroupID =
            NoticeID =
            FromName =
            Subject =
            Message =
            HasAttachment =

            optionally
            AttachmentType =
            AttachmentName =
            AttachmentItemID =
            AttachmentOwnerID =
            */
            UUI requestingAgent = UUI.Unknown;
            GroupNotice notice = new GroupNotice();
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
            }

            try
            {
                m_GroupsService.Notices.Add(requestingAgent, notice);
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
