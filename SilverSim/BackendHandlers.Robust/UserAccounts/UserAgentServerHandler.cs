// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.AuthInfo;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.GridUser;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.ServiceInterfaces.ServerParam;
using SilverSim.ServiceInterfaces.Traveling;
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.Friends;
using SilverSim.Types.Grid;
using SilverSim.Types.GridUser;
using SilverSim.Types.StructuredData.XmlRpc;
using SilverSim.Types.TravelingData;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.BackendHandlers.Robust.UserAccounts
{
    [Description("Robust UserAgent Protocol Server")]
    public class UserAgentServerHandler : IPlugin
    {
        GridServiceInterface m_GridService;
        GridUserServiceInterface m_GridUserService;
        UserAccountServiceInterface m_UserAccountService;
        PresenceServiceInterface m_PresenceService;
        TravelingDataServiceInterface m_TravelingDataService;
        AuthInfoServiceInterface m_AuthInfoService;

        readonly string m_GridServiceName;
        readonly string m_GridUserServiceName;
        readonly string m_UserAccountServiceName;
        readonly string m_PresenceServiceName;
        readonly string m_TravelingDataServiceName;
        readonly string m_AuthInfoServiceName;

        readonly Dictionary<string, string> m_ServiceURLs = new Dictionary<string, string>();
        static readonly string[] m_RequiredURLs = new string[] { "AssetServerURI", "InventoryServerURI", "IMServerURI", "FriendsServerURI", "ProfileServerURI" };

        public UserAgentServerHandler(IConfig ownConfig)
        {
            m_GridServiceName = ownConfig.GetString("GridService", "GridService");
            m_GridUserServiceName = ownConfig.GetString("GridUserService", "GridUserService");
            m_UserAccountServiceName = ownConfig.GetString("UserAccountService", "UserAccountService");
            m_PresenceServiceName = ownConfig.GetString("PresenceService", "PresenceService");
            m_TravelingDataServiceName = ownConfig.GetString("TravelingDataService", "TravelingDataService");
            m_AuthInfoServiceName = ownConfig.GetString("AuthInfoService", "AuthInfoService");
            foreach (string key in ownConfig.GetKeys())
            {
                if(key.StartsWith("SRV_"))
                {
                    m_ServiceURLs.Add(key.Substring(4), ownConfig.GetString(key));
                }
            }
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_ServiceURLs["HomeURI"] = loader.HomeURI;
            foreach(string requrl in m_RequiredURLs)
            {
                if(!m_ServiceURLs.ContainsKey(requrl))
                {
                    m_ServiceURLs.Add(requrl, loader.HomeURI);
                }
            }

            HttpXmlRpcHandler xmlRpc = loader.GetService<HttpXmlRpcHandler>("XmlRpcServer");
            xmlRpc.XmlRpcMethods.Add("verify_client", VerifyClient);
            xmlRpc.XmlRpcMethods.Add("verify_agent", VerifyAgent);
            xmlRpc.XmlRpcMethods.Add("get_uui", GetUUI);
            xmlRpc.XmlRpcMethods.Add("get_server_urls", GetServerURLs);
            xmlRpc.XmlRpcMethods.Add("get_home_region", GetHomeRegion);
            xmlRpc.XmlRpcMethods.Add("get_user_info", GetUserInfo);
            xmlRpc.XmlRpcMethods.Add("logout_agent", LogoutAgent);
            try
            {
                m_GridService = loader.GetService<GridServiceInterface>(m_GridServiceName);
            }
            catch
            {
                m_GridService = null;
            }

            try
            {
                m_GridUserService = loader.GetService<GridUserServiceInterface>(m_GridUserServiceName);
            }
            catch
            {
                m_GridUserService = null;
            }
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
            m_PresenceService = loader.GetService<PresenceServiceInterface>(m_PresenceServiceName);
            m_TravelingDataService = loader.GetService<TravelingDataServiceInterface>(m_TravelingDataServiceName);
            m_AuthInfoService = loader.GetService<AuthInfoServiceInterface>(m_AuthInfoServiceName);
        }

        public XmlRpc.XmlRpcResponse LogoutAgent(XmlRpc.XmlRpcRequest req)
        {
            Map reqdata;
            if (!req.Params.TryGetValue(0, out reqdata))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            UUID sessionId;
            if (!reqdata.TryGetValue("sessionID", out sessionId))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }
            UUID userId;
            if (!reqdata.TryGetValue("userID", out userId))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            Map respdata = new Map();
            try
            {
                TravelingDataInfo travelingInfo = m_TravelingDataService.GetTravelingData(sessionId);
                if(travelingInfo.UserID == userId)
                {
                    m_TravelingDataService.Remove(sessionId);
                }
                try
                {
                    m_GridUserService.LoggedOut(new UUI(userId), UUID.Zero, Vector3.Zero, Vector3.Zero);
                }
                catch
                {
                    /* ignore exception */
                }
                m_AuthInfoService.ReleaseTokenBySession(userId, sessionId);
                respdata.Add("result", "true");
            }
            catch
            {
                respdata.Add("result", "false");
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = respdata };
        }

        public XmlRpc.XmlRpcResponse VerifyClient(XmlRpc.XmlRpcRequest req)
        {
            Map reqdata;
            if (!req.Params.TryGetValue(0, out reqdata))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            UUID id;
            if (!reqdata.TryGetValue("sessionID", out id))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }
            string token;
            if (!reqdata.TryGetValue("token", out token))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            Map respdata = new Map();
            try
            {
                TravelingDataInfo travelingData = m_TravelingDataService.GetTravelingData(id);
                respdata.Add("result", travelingData.ClientIPAddress == token ? "true" : "false");
            }
            catch
            {
                respdata.Add("result", "false");
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = respdata };
        }

        public XmlRpc.XmlRpcResponse VerifyAgent(XmlRpc.XmlRpcRequest req)
        {
            Map reqdata;
            if (!req.Params.TryGetValue(0, out reqdata))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            UUID id;
            if (!reqdata.TryGetValue("sessionID", out id))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }
            string token;
            if (!reqdata.TryGetValue("token", out token))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }
            string[] tokendata = token.Split(';');
            if (tokendata.Length != 2)
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            if(!tokendata[0].EndsWith("/"))
            {
                tokendata[0] += "/";
            }

            Map respdata = new Map();
            try
            {
                TravelingDataInfo travelingData = m_TravelingDataService.GetTravelingData(id);
                respdata.Add("result", (tokendata[0] == travelingData.GridExternalName && tokendata[1] == travelingData.ServiceToken) ? "true" : " false");
            }
            catch
            {
                respdata.Add("result", "false");
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = respdata };
        }

        public XmlRpc.XmlRpcResponse GetUserInfo(XmlRpc.XmlRpcRequest req)
        {
            Map reqdata;
            if(!req.Params.TryGetValue(0, out reqdata))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            UUID id;
            if(!reqdata.TryGetValue("userID", out id))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            UserAccount account;
            Map resdata = new Map();
            if(m_UserAccountService.TryGetValue(UUID.Zero, id, out account))
            {
                resdata.Add("user_firstname", account.Principal.FirstName);
                resdata.Add("user_lastname", account.Principal.LastName);
                resdata.Add("user_flags", account.UserFlags);
                resdata.Add("user_created", account.Created.AsULong);
                resdata.Add("user_title", account.UserTitle);
                resdata.Add("result", "success");
            }
            else
            {
                resdata.Add("result", "failure");
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        public XmlRpc.XmlRpcResponse GetUUI(XmlRpc.XmlRpcRequest req)
        {
            Map reqdata;
            if(!req.Params.TryGetValue(0, out reqdata))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }
            UUID toid;
            UUID fromid;
            if(!reqdata.TryGetValue("targetUserID", out toid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }
            if (!reqdata.TryGetValue("userID", out fromid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            UserAccount account;
            UUI uui = null;
            GridUserInfo userInfo;
            if(m_UserAccountService.TryGetValue(UUID.Zero, toid, out account))
            {
                uui = account.Principal;
            }
            else if(m_GridUserService.TryGetValue(toid, out userInfo))
            {
                if(userInfo.User.HomeURI != null)
                {
                    uui = userInfo.User;
                }
            }

            Map respdata = new Map();
            if (null != uui)
            {
                respdata.Add("UUI", uui.ToString());
            }
            else
            { 
                respdata.Add("result", "User unknown");
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = respdata };
        }

        public XmlRpc.XmlRpcResponse GetServerURLs(XmlRpc.XmlRpcRequest req)
        {
            Map respdata = new Map();
            foreach(KeyValuePair<string, string> kvp in m_ServiceURLs)
            {
                respdata.Add("SRV_" + kvp.Key, kvp.Value);
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = respdata };
        }

        public XmlRpc.XmlRpcResponse GetHomeRegion(XmlRpc.XmlRpcRequest req)
        {
            Map respdata = new Map();

            RegionInfo ri = null;
            GridUserInfo userInfo = null;
            Map reqdata;

            if(!req.Params.TryGetValue(0, out reqdata))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            UUID userID;
            if(!reqdata.TryGetValue("userID", out userID))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "invalid method parameters");
            }

            if (null != m_GridService && null != m_GridUserService)
            {
                if(!m_GridUserService.TryGetValue(userID, out userInfo) ||
                    !m_GridService.TryGetValue(userInfo.HomeRegionID, out ri))
                {
                    ri = null;
                }
            }

            if(null == ri)
            {
                respdata.Add("result", "false");
            }
            else
            {
                respdata.Add("uuid", ri.ID);
                respdata.Add("x", ri.Location.X);
                respdata.Add("y", ri.Location.Y);
                respdata.Add("region_name", ri.Name);
                respdata.Add("hostname", ri.ServerIP);
                respdata.Add("http_port", ri.ServerHttpPort);
                respdata.Add("internal_port", ri.ServerPort);
                respdata.Add("server_uri", ri.ServerURI);
                respdata.Add("result", "true");
                respdata.Add("position", userInfo.HomePosition.ToString());
                respdata.Add("lookAt", userInfo.HomeLookAt.ToString());
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = respdata };
        }
    }

    [PluginName("UserAgentServer")]
    public class UserAgentServerHandlerFactory : IPluginFactory
    {
        public UserAgentServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new UserAgentServerHandler(ownSection);
        }
    }
}
