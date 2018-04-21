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
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.GridUser;
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.GridUser;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.GridUser
{
    [Description("Robust GridUser Protocol Server")]
    [PluginName("GridUserHandler")]
    public sealed class RobustGridUserServerHandler : IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRIDUSER HANDLER");
        private BaseHttpServer m_HttpServer;
        private GridUserServiceInterface m_GridUserService;
        private AvatarNameServiceInterface m_AvatarNameService;
        private UserAccountServiceInterface m_UserAccountService;
        private readonly string m_GridUserServiceName;
        private readonly string m_UserAccountServiceName;
        private readonly string m_AvatarNameStorageName;

        public RobustGridUserServerHandler(IConfig ownSection)
        {
            m_GridUserServiceName = ownSection.GetString("GridUserService", "GridUserService");
            m_UserAccountServiceName = ownSection.GetString("UserAccountService", "UserAccountService");
            m_AvatarNameStorageName = ownSection.GetString("AvatarNameStorage", "AvatarNameStorage");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for GridUser server");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.UriHandlers.Add("/griduser", GridUserHandler);
            m_GridUserService = loader.GetService<GridUserServiceInterface>(m_GridUserServiceName);
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
            m_AvatarNameService = loader.GetService<AvatarNameServiceInterface>(m_AvatarNameStorageName);
            BaseHttpServer https;
            if(loader.TryGetHttpsServer(out https))
            {
                https.UriHandlers.Add("/griduser", GridUserHandler);
            }
        }

        private void GridUserHandler(HttpRequest req)
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
                    req.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                    break;
            }
        }

        private readonly byte[] SuccessResult = "<?xml version=\"1.0\"?><ServerResponse><result>Success</result></ServerResponse>".ToUTF8Bytes();
        private readonly byte[] FailureResult = "<?xml version=\"1.0\"?><ServerResponse><result>Failure</result></ServerResponse>".ToUTF8Bytes();

        public void PostGridUserHandler(HttpRequest req)
        {
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

        private UGUIWithName FindUser(string userID)
        {
            var uui = new UGUIWithName(userID);
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
                return m_AvatarNameService.ResolveName(ui.User);
            }
        }

        private void LoggedIn(Dictionary<string, object> req)
        {
            m_GridUserService.LoggedIn(FindUser(req["UserID"].ToString()));
        }

        private void LoggedOut(Dictionary<string, object> req)
        {
            var region = new UUID(req["RegionID"].ToString());
            Vector3 position = Vector3.Parse(req["Position"].ToString());
            Vector3 lookAt = Vector3.Parse(req["LookAt"].ToString());

            m_GridUserService.LoggedOut(FindUser(req["UserID"].ToString()), region, position, lookAt);
        }

        private void SetHome(Dictionary<string, object> req)
        {
            var region = new UUID(req["RegionID"].ToString());
            Vector3 position = Vector3.Parse(req["Position"].ToString());
            Vector3 lookAt = Vector3.Parse(req["LookAt"].ToString());

            m_GridUserService.SetHome(FindUser(req["UserID"].ToString()), region, position, lookAt);
        }

        private void SetPosition(Dictionary<string, object> req)
        {
            var region = new UUID(req["RegionID"].ToString());
            Vector3 position = Vector3.Parse(req["Position"].ToString());
            Vector3 lookAt = Vector3.Parse(req["LookAt"].ToString());

            m_GridUserService.SetPosition(FindUser(req["UserID"].ToString()), region, position, lookAt);
        }

        #region getgriduserinfo
        private void WriteXmlGridUserEntry(XmlTextWriter w, GridUserInfo ui, string outerTagName)
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

        private void WriteXmlGridUserEntry(XmlTextWriter w, UGUIWithName ui, string outerTagName)
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

        private UGUIWithName CheckGetUUI(Dictionary<string, object> req, HttpRequest httpreq)
        {
            try
            {
                return FindUser(req["UserID"].ToString());
            }
            catch
            {
                /* check for avatarnames service */
                UGUIWithName aui;
                try
                {
                    aui = m_AvatarNameService[new UGUIWithName(req["UserID"].ToString())];
                }
                catch
                {
                    aui = null;
                }

                if(aui != null)
                {
                    using(HttpResponse resp = httpreq.BeginResponse("text/xml"))
                    {
                        using (XmlTextWriter writer = resp.GetOutputStream().UTF8XmlTextWriter())
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
                        using (XmlTextWriter writer = resp.GetOutputStream().UTF8XmlTextWriter())
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

        private bool WriteUserInfo(XmlTextWriter writer, UGUIWithName uui, string outertagname, bool writeNullEntry)
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

        private void GetGridUserInfo(Dictionary<string, object> req, HttpRequest httpreq)
        {
            UGUIWithName uui = CheckGetUUI(req, httpreq);
            if(uui == null)
            {
                return;
            }

            using (HttpResponse resp = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = resp.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    WriteUserInfo(writer, uui, "result", true);
                    writer.WriteEndElement();
                }
            }
        }

        private void GetGridUserInfos(Dictionary<string, object> req, HttpRequest httpreq)
        {
            bool anyFound = false;
            using (HttpResponse resp = httpreq.BeginResponse("text/xml"))
            {
                using(XmlTextWriter writer = resp.GetOutputStream().UTF8XmlTextWriter())
                {
                    var userIDs = (List<string>)req["AgentIDs"];

                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("result");
                    int index = 0;
                    foreach (string userID in userIDs)
                    {
                        UGUIWithName uui;

                        try
                        {
                            uui = FindUser(userID);
                        }
                        catch
                        {
                            try
                            {
                                uui = m_AvatarNameService[new UGUIWithName(userID)];
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
}
