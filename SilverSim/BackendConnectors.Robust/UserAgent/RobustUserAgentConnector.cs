// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common;
using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.UserAgents;
using SilverSim.Types;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.BackendConnectors.Robust.UserAgent
{
    [Description("Robust UserAgent Connector")]
    public class RobustUserAgentConnector : UserAgentServiceInterface, IPlugin
    {
        public int TimeoutMs = 20000;
        readonly string m_Uri;

        public RobustUserAgentConnector(string uri)
        {
            m_Uri = uri;
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public override void VerifyAgent(UUID sessionID, string token)
        {
            Map hash = new Map();
            hash.Add("sessionID", sessionID);
            hash.Add("token", token);
            DoXmlRpcWithBoolResponse("verify_agent", hash);
        }

        public override void VerifyClient(UUID sessionID, string token)
        {
            Map hash = new Map();
            hash.Add("sessionID", sessionID);
            hash.Add("token", token);
            DoXmlRpcWithBoolResponse("verify_client", hash);
        }

        public override List<UUID> NotifyStatus(List<KeyValuePair<UUI, string>> friends, UUI user, bool online)
        {
            Map hash = new Map();
            hash.Add("userID", user.ID);
            hash.Add("online", online.ToString());
            int i = 0;
            foreach(KeyValuePair<UUI, string> s in friends)
            {
                hash.Add("friend_" + i.ToString(), s.Key.ToString() + ";" + s.Value);
                ++i;
            }

            Map res = DoXmlRpcWithHashResponse("status_notification", hash);

            List<UUID> friendsOnline = new List<UUID>();

            foreach(string key in res.Keys)
            {
                if(key.StartsWith("friend_") && res[key] != null)
                {
                    UUID friend;
                    if(UUID.TryParse(res[key].ToString(), out friend))
                    {
                        friendsOnline.Add(friend);
                    }
                }
            }

            return friendsOnline;
        }

        public override UserAgentServiceInterface.UserInfo GetUserInfo(UUI user)
        {
            Dictionary<string, string> info = GetUserInfo_Internal(user);
            UserInfo userInfo = new UserInfo();
            userInfo.FirstName = info.ContainsKey("user_firstname") ? info["user_firstname"] : user.FirstName;
            userInfo.LastName = info.ContainsKey("user_lastname") ? info["user_lastname"] : user.LastName;

            if (info.ContainsKey("user_flags"))
            {
                userInfo.UserFlags = uint.Parse(info["user_flags"]);
            }
            
            if(info.ContainsKey("user_created"))
            {
                userInfo.UserCreated = Date.UnixTimeToDateTime(ulong.Parse(info["user_created"]));
            }
            
            if(info.ContainsKey("user_title"))
            {
                userInfo.UserTitle = info["user_title"];
            }

            return userInfo;
        }

        Dictionary<string, string> GetUserInfo_Internal(UUI user)
        {
            Map hash = new Map();
            hash.Add("userID", user.ID);
            
            Map res = DoXmlRpcWithHashResponse("get_user_info", hash);
            Dictionary<string, string> info = new Dictionary<string, string>();
            foreach (string key in res.Keys)
            {
                if (res[key] != null)
                {
                    info.Add(key, res[key].ToString());
                }
            }

            return info;
        }

        public override Dictionary<string, string> GetServerURLs(UUI user)
        {
            Map hash = new Map();
            hash.Add("userID", user.ID);

            Map res = DoXmlRpcWithHashResponse("get_server_urls", hash);
            Dictionary<string, string> serverUrls = new Dictionary<string, string>();
            foreach (string key in res.Keys)
            {
                if(key.StartsWith("SRV_") && res[key] != null)
                {
                    string serverType = key.Substring(4);
                    serverUrls.Add(serverType, res[key].ToString());
                }
            }

            return serverUrls;
        }

        public override string LocateUser(UUI user)
        {
            Map hash = new Map();
            hash.Add("userID", user.ID);

            Map res = DoXmlRpcWithHashResponse("locate_user", hash);

            if(res.ContainsKey("URL"))
            {
                return res["URL"].ToString();
            }

            throw new KeyNotFoundException();
        }

        public override UUI GetUUI(UUI user, UUI targetUserID)
        {
            Map hash = new Map();
            hash.Add("userID", user.ID);
            hash.Add("targetUserID", targetUserID.ID);

            Map res = DoXmlRpcWithHashResponse("get_uui", hash);

            if (res.ContainsKey("UUI"))
            {
                return new UUI(res["UUI"].ToString());
            }

            throw new KeyNotFoundException();
        }

        public override DestinationInfo GetHomeRegion(UUI user)
        {
            throw new NotSupportedException();
        }

        void DoXmlRpcWithBoolResponse(string method, Map reqparams)
        {
            XmlRpc.XmlRpcRequest req = new XmlRpc.XmlRpcRequest(method);
            req.Params.Add(reqparams);
            XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);

            Map hash = (Map)res.ReturnValue;
            if(hash == null)
            {
                throw new InvalidOperationException();
            }

            bool success = false;
            if (hash.ContainsKey("result"))
            {
                success = Boolean.Parse(hash["result"].ToString());
                if(!success)
                {
                    throw new RequestFailedException();
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        Map DoXmlRpcWithHashResponse(string method, Map reqparams)
        {
            XmlRpc.XmlRpcRequest req = new XmlRpc.XmlRpcRequest(method);
            req.Params.Add(reqparams);
            XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);

            Map hash = (Map)res.ReturnValue;
            if (hash == null)
            {
                throw new InvalidOperationException();
            }

            return hash;
        }
    }
}
