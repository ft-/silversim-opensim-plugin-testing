// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.UserAgents;
using SilverSim.Types;
using SilverSim.Types.StructuredData.XMLRPC;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.UserAgent
{
    public class RobustUserAgentConnector : UserAgentServiceInterface
    {
        public int TimeoutMs = 20000;
        string m_Uri;

        public RobustUserAgentConnector(string uri)
        {
            m_Uri = uri;
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

            foreach(string key in hash.Keys)
            {
                if(key.StartsWith("friend_") && hash[key] != null)
                {
                    UUID friend;
                    if(UUID.TryParse(hash[key].ToString(), out friend))
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
            if (info.ContainsKey("user_firstname"))
            {
                userInfo.FirstName = info["user_firstname"];
            }
            else
            {
                userInfo.FirstName = user.FirstName;
            }
            
            if (info.ContainsKey("user_lastname"))
            {
                userInfo.LastName = info["user_lastname"];
            }
            else
            {
                userInfo.LastName = user.LastName;
            }

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
                    info.Add(key.ToString(), res[key].ToString());
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
                    string serverType = key.ToString().Substring(4);
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

            if(hash.ContainsKey("URL"))
            {
                return hash["URL"].ToString();
            }

            throw new KeyNotFoundException();
        }

        public override UUI GetUUI(UUI user, UUI targetUserID)
        {
            Map hash = new Map();
            hash.Add("userID", user.ID);
            hash.Add("targetUserID", targetUserID.ID);

            Map res = DoXmlRpcWithHashResponse("get_uui", hash);

            if (hash.ContainsKey("UUI"))
            {
                return new UUI(hash["UUI"].ToString());
            }

            throw new KeyNotFoundException();
        }

        void DoXmlRpcWithBoolResponse(string method, Map reqparams)
        {
            XMLRPC.XmlRpcRequest req = new XMLRPC.XmlRpcRequest(method);
            req.Params.Add(reqparams);
            XMLRPC.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);

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
            XMLRPC.XmlRpcRequest req = new XMLRPC.XmlRpcRequest(method);
            req.Params.Add(reqparams);
            XMLRPC.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);

            Map hash = (Map)res.ReturnValue;
            if (hash == null)
            {
                throw new InvalidOperationException();
            }

            return hash;
        }
    }
}
