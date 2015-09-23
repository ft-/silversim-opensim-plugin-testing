// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.ServerParam;
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.UserAccounts
{
    #region Service Implementation
    static class ExtensionMethods
    {
        public static void WriteUserAccount(this XmlTextWriter writer, string tagname, UserAccount ua)
        {
            writer.WriteStartElement(tagname);
            {
                writer.WriteAttributeString("type", "List");
                writer.WriteNamedValue("FirstName", ua.Principal.FirstName);
                writer.WriteNamedValue("LastName", ua.Principal.LastName);
                writer.WriteNamedValue("Email", ""); /* keep empty for privacy */
                writer.WriteNamedValue("PrincipalID", ua.Principal.ID);
                writer.WriteNamedValue("ScopeID", ua.ScopeID);
                writer.WriteNamedValue("Created", ua.Created.DateTimeToUnixTime());
                writer.WriteNamedValue("UserLevel", ua.UserLevel);
                writer.WriteNamedValue("UserFlags", ua.UserFlags);
                writer.WriteNamedValue("UserTitle", ua.UserTitle);
                writer.WriteNamedValue("LocalToGrid", ua.IsLocalToGrid);

                string str = string.Empty;
                foreach(KeyValuePair<string, string> kvp in ua.ServiceURLs)
                {
                    str += kvp.Key + "*" + (string.IsNullOrEmpty(kvp.Value) ? "" : kvp.Value) + ";";
                }
                writer.WriteNamedValue("ServiceURLs", str);
            }
            writer.WriteEndElement();
        }
    }

    class RobustUserAccountServerHandler : IPlugin
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST USERACCOUNT HANDLER");
        private BaseHttpServer m_HttpServer;
        UserAccountServiceInterface m_UserAccountService = null;
        string m_UserAccountServiceName;
        static Encoding UTF8NoBOM = new System.Text.UTF8Encoding(false);

        public RobustUserAccountServerHandler(string userAccountServiceName)
        {
            m_UserAccountServiceName = userAccountServiceName;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing handler for UserAccount server");
            m_HttpServer = loader.HttpServer;
            m_HttpServer.StartsWithUriHandlers.Add("/accounts", UserAccountHandler);
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
        }

        public void UserAccountHandler(HttpRequest req)
        {
            if (req.ContainsHeader("X-SecondLife-Shard"))
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Request source not allowed");
                return;
            }

            switch (req.Method)
            {
                case "POST":
                    PostUserAccountHandler(req);
                    break;

                default:
                    req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
                    break;
            }
        }

        readonly byte[] SuccessResult = UTF8NoBOM.GetBytes("<?xml version=\"1.0\"?><ServerResponse><result>Success</result></ServerResponse>");
        readonly byte[] FailureResult = UTF8NoBOM.GetBytes("<?xml version=\"1.0\"?><ServerResponse><result>Failure</result></ServerResponse>");

        public void PostUserAccountHandler(HttpRequest req)
        {
            Dictionary<string, object> data;
            try
            {
                data = REST.parseREST(req.Body);
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            try
            {
                switch (data["METHOD"].ToString())
                {
                    case "getaccount":
                        GetAccount(req, data);
                        break;

                    case "getaccounts":
                        GetAccounts(req, data);
                        break;

                    default:
                        req.ErrorResponse(HttpStatusCode.BadRequest, "Unknown Accounts method");
                        return;
                }
            }
            catch (HttpResponse.ConnectionCloseException)
            {
                throw;
            }
            catch
            {
                using (HttpResponse res = req.BeginResponse("text/xml"))
                {
                    res.GetOutputStream(FailureResult.Length).Write(FailureResult, 0, FailureResult.Length);
                }
            }
        }

        public void GetAccount(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID scopeID = reqdata.GetUUID("ScopeID");
            UserAccount ua = null;

            if(reqdata.ContainsKey("UserID"))
            {
                UUID userID = reqdata.GetUUID("UserID");
                try
                {
                    ua = m_UserAccountService[scopeID, userID];
                }
                catch
                {

                }
            }
            else if(reqdata.ContainsKey("PrincipalID"))
            {
                UUID userID = reqdata.GetUUID("UserID");
                try
                {
                    ua = m_UserAccountService[scopeID, userID];
                }
                catch
                {

                }
            }
            else if(reqdata.ContainsKey("Email"))
            {
                string email = reqdata.GetString("Email");
                try
                {
                    ua = m_UserAccountService[scopeID, email];
                }
                catch
                {

                }
            }
            else if(reqdata.ContainsKey("FirstName") && reqdata.ContainsKey("LastName"))
            {
                string firstName = reqdata.GetString("FirstName");
                string lastName = reqdata.GetString("LastName");
                try
                {
                    ua = m_UserAccountService[scopeID, firstName, lastName];
                }
                catch
                {

                }
            }

            using(HttpResponse res = req.BeginResponse("text/xml"))
            {
                using(XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
                {
                    writer.WriteStartElement("ServerResponse");
                    if(ua == null)
                    {
                        writer.WriteStartElement("result");
                        writer.WriteValue("null");
                        writer.WriteEndElement();
                    }
                    else
                    {
                        writer.WriteUserAccount("result", ua);
                    }
                    writer.WriteEndElement();
                }
            }
        }

        public void GetAccounts(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID scopeID = reqdata.GetUUID("ScopeID");
            string query = reqdata.GetString("query");

            List<UserAccount> accounts;
            try
            {
                accounts = m_UserAccountService.GetAccounts(scopeID, query);
            }
            catch
            {
                accounts = new List<UserAccount>();
            }

            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = new XmlTextWriter(res.GetOutputStream(), UTF8NoBOM))
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("result");
                    if (accounts.Count == 0)
                    {
                        writer.WriteValue("null");
                    }
                    else
                    {
                        writer.WriteAttributeString("type", "List");
                        int i = 0;
                        foreach (UserAccount ua in accounts)
                        {
                            writer.WriteUserAccount("account" + i, ua);
                            ++i;
                        }
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("UserAccountHandler")]
    public class RobustUserAccountServerHandlerFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST USERACCOUNT HANDLER");
        public RobustUserAccountServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustUserAccountServerHandler(ownSection.GetString("UserAccountService", "UserAccountService"));
        }
    }
    #endregion
}
