// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.GridUser;
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.GridUser;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.GridUser
{
    #region Service Implementation
    public sealed class RobustGridUserServerHandler : IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRIDUSER HANDLER");
        private BaseHttpServer m_HttpServer;
        GridUserServiceInterface m_GridUserService;
        AvatarNameServiceInterface m_AvatarNameService;
        UserAccountServiceInterface m_UserAccountService;
        string m_GridUserServiceName;
        string m_UserAccountServiceName;
        string m_AvatarNameStorageName;
        private static Encoding UTF8NoBOM = new System.Text.UTF8Encoding(false);

        public RobustGridUserServerHandler(string gridUserService, string userAccountService, string avatarNameService)
        {
            m_GridUserServiceName = gridUserService;
            m_UserAccountServiceName = userAccountService;
            m_AvatarNameStorageName = avatarNameService;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for GridUser server");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.StartsWithUriHandlers.Add("/griduser", GridUserHandler);
            m_GridUserService = loader.GetService<GridUserServiceInterface>(m_GridUserServiceName);
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
            m_AvatarNameService = loader.GetService<AvatarNameServiceInterface>(m_AvatarNameStorageName);
        }

        public void GridUserHandler(HttpRequest req)
        {
            if (req.ContainsHeader("X-SecondLife-Shard"))
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Request source not allowed");
                return;
            }

            switch(req.Method)
            {
                case "POST":
                    PostGridUserHandler(req);
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
                    break;
            }
        }

        readonly byte[] SuccessResult = UTF8NoBOM.GetBytes("<?xml version=\"1.0\"?><ServerResponse><result>Success</result></ServerResponse>");
        readonly byte[] FailureResult = UTF8NoBOM.GetBytes("<?xml version=\"1.0\"?><ServerResponse><result>Failure</result></ServerResponse>");

        public void PostGridUserHandler(HttpRequest req)
        {
            Dictionary<string, object> data;
            try
            {
                data = REST.ParseREST(req.Body);
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            try
            {
                switch(data["METHOD"].ToString())
                {
                    case "loggedin":
                        LoggedIn(data);
                        break;

                    case "loggedout":
                        LoggedOut(data);
                        break;

                    case "sethome":
                        SetHome(data);
                        break;

                    case "setposition":
                        SetPosition(data);
                        break;

                    case "getgriduserinfo":
                        GetGridUserInfo(data, req);
                        return;
                        
                    case "getgriduserinfos":
                        GetGridUserInfos(data, req);
                        return;

                    default:
                        req.ErrorResponse(HttpStatusCode.BadRequest, "Unknown GridUser method");
                        return;
                }


                using (HttpResponse res = req.BeginResponse())
                {
                    res.ContentType = "text/xml";
                    using (Stream s = res.GetOutputStream(SuccessResult.Length))
                    {
                        s.Write(SuccessResult, 0, SuccessResult.Length);
                    }
                }
            }
            catch(HttpResponse.ConnectionCloseException)
            {
                throw;
            }
            catch
            {
                using (HttpResponse res = req.BeginResponse())
                {
                    res.ContentType = "text/xml";
                    using (Stream s = res.GetOutputStream(FailureResult.Length))
                    {
                        s.Write(FailureResult, 0, FailureResult.Length);
                    }
                }
            }
        }

        UUI FindUser(string userID)
        {
            UUI uui = new UUI(userID);
            try
            {
                UserAccount account = m_UserAccountService[UUID.Zero, uui.ID];
                return account.Principal;
            }
            catch
            {
                GridUserInfo ui = m_GridUserService[uui];
                if (!ui.User.IsAuthoritative)
                {
                    throw new GridUserNotFoundException();
                }
                return ui.User;
            }
        }

        public void LoggedIn(Dictionary<string, object> req)
        {
            m_GridUserService.LoggedIn(FindUser(req["UserID"].ToString()));
        }

        public void LoggedOut(Dictionary<string, object> req)
        {
            UUID region = new UUID(req["RegionID"].ToString());
            Vector3 position = Vector3.Parse(req["Position"].ToString());
            Vector3 lookAt = Vector3.Parse(req["LookAt"].ToString());

            m_GridUserService.LoggedOut(FindUser(req["UserID"].ToString()), region, position, lookAt);
        }

        public void SetHome(Dictionary<string, object> req)
        {
            UUID region = new UUID(req["RegionID"].ToString());
            Vector3 position = Vector3.Parse(req["Position"].ToString());
            Vector3 lookAt = Vector3.Parse(req["LookAt"].ToString());

            m_GridUserService.SetHome(FindUser(req["UserID"].ToString()), region, position, lookAt);
        }

        public void SetPosition(Dictionary<string, object> req)
        {
            UUID region = new UUID(req["RegionID"].ToString());
            Vector3 position = Vector3.Parse(req["Position"].ToString());
            Vector3 lookAt = Vector3.Parse(req["LookAt"].ToString());

            m_GridUserService.SetPosition(FindUser(req["UserID"].ToString()), region, position, lookAt);
        }

        #region getgriduserinfo
        void WriteXmlGridUserEntry(XmlTextWriter w, GridUserInfo ui, string outerTagName)
        {
            w.WriteStartElement(outerTagName);
            w.WriteNamedValue("UserID", (string)ui.User);
            w.WriteNamedValue("HomeRegionID", ui.HomeRegionID);
            w.WriteNamedValue("HomePosition", ui.HomePosition.ToString());
            w.WriteNamedValue("HomeLookAt", ui.HomeLookAt.ToString());
            w.WriteNamedValue("LastRegionID", ui.LastRegionID);
            w.WriteNamedValue("LastPosition", ui.LastPosition.ToString());
            w.WriteNamedValue("LastLookAt", ui.LastLookAt.ToString());
            w.WriteNamedValue("Online", ui.IsOnline.ToString());
            w.WriteNamedValue("Login", ui.LastLogin.DateTimeToUnixTime());
            w.WriteNamedValue("Logout", ui.LastLogout.DateTimeToUnixTime());
            w.WriteEndElement();
        }

        void WriteXmlGridUserEntry(XmlTextWriter w, UUI ui, string outerTagName)
        {
            w.WriteStartElement(outerTagName);
            w.WriteNamedValue("UserID", (string)ui);
            w.WriteNamedValue("HomeRegionID", UUID.Zero);
            w.WriteNamedValue("HomePosition", Vector3.Zero);
            w.WriteNamedValue("HomeLookAt", Vector3.Zero);
            w.WriteNamedValue("LastRegionID", UUID.Zero);
            w.WriteNamedValue("LastPosition", Vector3.Zero);
            w.WriteNamedValue("LastLookAt", Vector3.Zero);
            w.WriteNamedValue("Online", false);
            w.WriteNamedValue("Login", 0);
            w.WriteNamedValue("Logout", 0);
            w.WriteEndElement();
        }

        public UUI CheckGetUUI(Dictionary<string, object> req, HttpRequest httpreq)
        {
            try
            {
                return FindUser(req["UserID"].ToString());
            }
            catch
            {
                /* check for avatarnames service */
                UUI aui;
                try
                {
                    aui = m_AvatarNameService[new UUI(req["UserID"].ToString())];
                }
                catch
                {
                    aui = null;
                }

                if(null != aui)
                {
                    using(HttpResponse resp = httpreq.BeginResponse("text/xml"))
                    {
                        using (XmlTextWriter writer = new XmlTextWriter(resp.GetOutputStream(), UTF8NoBOM))
                        {
                            writer.WriteStartElement("ServerResponse");
                            WriteXmlGridUserEntry(writer, aui, "result");
                            writer.WriteEndElement();
                        }
                    }
                }
                else
                {
                    using (HttpResponse resp = httpreq.BeginResponse("text/xml"))
                    {
                        using (XmlTextWriter writer = new XmlTextWriter(resp.GetOutputStream(), UTF8NoBOM))
                        {
                            writer.WriteStartElement("ServerResponse");
                            writer.WriteStartElement("result");
                            writer.WriteValue("null");
                            writer.WriteEndElement();
                            writer.WriteEndElement();
                        }
                    }
                }
            }
            return null;
        }

        bool WriteUserInfo(XmlTextWriter writer, UUI uui, string outertagname, bool writeNullEntry)
        {
            if (uui.HomeURI == null)
            {
                /* this one is grid local, so only try to take missing data */
                try
                {
                    GridUserInfo ui = m_GridUserService[uui];
                    WriteXmlGridUserEntry(writer, ui, outertagname);
                }
                catch
                {
                    WriteXmlGridUserEntry(writer, uui, outertagname);
                }
            }
            else
            {
                /* this one is grid foreign, so take AvatarNames and/or GridUser */
                try
                {
                    GridUserInfo ui = m_GridUserService[uui];
                    WriteXmlGridUserEntry(writer, ui, outertagname);
                }
                catch
                {
                    try
                    {
                        uui = m_AvatarNameService[uui];
                        WriteXmlGridUserEntry(writer, uui, outertagname);
                    }
                    catch
                    {
                        if (writeNullEntry)
                        {
                            /* should not happen but better be defensive here */
                            writer.WriteStartElement(outertagname);
                            writer.WriteValue("null");
                            writer.WriteEndElement();
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public void GetGridUserInfo(Dictionary<string, object> req, HttpRequest httpreq)
        {
            UUI uui = CheckGetUUI(req, httpreq);
            if(null == uui)
            {
                return;
            }

            using (HttpResponse resp = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = new XmlTextWriter(resp.GetOutputStream(), UTF8NoBOM))
                {
                    writer.WriteStartElement("ServerResponse");
                    WriteUserInfo(writer, uui, "result", true);
                    writer.WriteEndElement();
                }
            }
        }

        public void GetGridUserInfos(Dictionary<string, object> req, HttpRequest httpreq)
        {
            bool anyFound = false;
            using (HttpResponse resp = httpreq.BeginResponse("text/xml"))
            {
                using(XmlTextWriter writer = new XmlTextWriter(resp.GetOutputStream(), UTF8NoBOM))
                {
                    List<string> userIDs = (List<string>)req["AgentIDs"];

                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("result");
                    int index = 0;
                    foreach (string userID in userIDs)
                    {
                        UUI uui;

                        try
                        {
                            uui = FindUser(userID);
                        }
                        catch
                        {
                            try
                            {
                                uui = m_AvatarNameService[new UUI(userID)];
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        bool found = WriteUserInfo(writer, uui, "griduser" + index.ToString(), false);
                        if (found)
                        {
                            ++index;
                        }
                        anyFound = anyFound || found;
                    }
                    if (!anyFound)
                    {
                        writer.WriteValue("null");
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
        }
        #endregion
    }
    #endregion

    #region Factory
    [PluginName("GridUserHandler")]
    public class RobustGridUserHandlerFactory : IPluginFactory
    {
        public RobustGridUserHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustGridUserServerHandler(ownSection.GetString("GridUserService", "GridUserService"),
                ownSection.GetString("UserAccountService", "UserAccountService"),
                ownSection.GetString("AvatarNameStorage", "AvatarNameStorage"));
        }
    }
    #endregion
}
