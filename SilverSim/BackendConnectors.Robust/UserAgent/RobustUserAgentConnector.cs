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

using SilverSim.Main.Common;
using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.UserAgents;
using SilverSim.Types;
using SilverSim.Types.Grid;
using SilverSim.Types.ServerURIs;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;

namespace SilverSim.BackendConnectors.Robust.UserAgent
{
    [Description("Robust UserAgent Connector")]
    public class RobustUserAgentConnector : UserAgentServiceInterface, IPlugin, IDisplayNameAccessor
    {
        public int TimeoutMs = 20000;
        private readonly string m_Uri;

        public override IDisplayNameAccessor DisplayName => this;

        string IDisplayNameAccessor.this[UUI agent]
        {
            get { throw new KeyNotFoundException(); }

            set { throw new NotSupportedException(); }
        }

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
            var hash = new Map
            {
                { "sessionID", sessionID },
                { "token", token }
            };
            DoXmlRpcWithBoolResponse("verify_agent", hash);
        }

        public override void VerifyClient(UUID sessionID, string token)
        {
            var hash = new Map
            {
                { "sessionID", sessionID },
                { "token", token }
            };
            DoXmlRpcWithBoolResponse("verify_client", hash);
        }

        public override List<UUID> NotifyStatus(List<KeyValuePair<UUI, string>> friends, UUI user, bool online)
        {
            var hash = new Map
            {
                { "userID", user.ID },
                { "online", online.ToString() }
            };
            int i = 0;
            foreach(KeyValuePair<UUI, string> s in friends)
            {
                hash.Add("friend_" + i.ToString(), s.Key.ToString() + ";" + s.Value);
                ++i;
            }

            Map res = DoXmlRpcWithHashResponse("status_notification", hash);

            var friendsOnline = new List<UUID>();

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
            var userInfo = new UserInfo()
            {
                FirstName = info.ContainsKey("user_firstname") ? info["user_firstname"] : user.FirstName,
                LastName = info.ContainsKey("user_lastname") ? info["user_lastname"] : user.LastName
            };
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

        private Dictionary<string, string> GetUserInfo_Internal(UUI user)
        {
            var hash = new Map
            {
                ["userID"] = user.ID
            };
            Map res = DoXmlRpcWithHashResponse("get_user_info", hash);
            var info = new Dictionary<string, string>();
            foreach (string key in res.Keys)
            {
                if (res[key] != null)
                {
                    info.Add(key, res[key].ToString());
                }
            }

            return info;
        }

        public override ServerURIs GetServerURLs(UUI user)
        {
            var hash = new Map
            {
                ["userID"] = user.ID
            };
            Map res = DoXmlRpcWithHashResponse("get_server_urls", hash);
            var serverUrls = new ServerURIs();
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
            var hash = new Map
            {
                ["userID"] = user.ID
            };
            var res = DoXmlRpcWithHashResponse("locate_user", hash);

            if(res.ContainsKey("URL"))
            {
                return res["URL"].ToString();
            }

            throw new KeyNotFoundException();
        }

        public override UUI GetUUI(UUI user, UUI targetUserID)
        {
            var hash = new Map
            {
                ["userID"] = user.ID,
                ["targetUserID"] = targetUserID.ID
            };
            Map res = DoXmlRpcWithHashResponse("get_uui", hash);

            if (res.ContainsKey("UUI"))
            {
                return new UUI(res["UUI"].ToString());
            }

            throw new KeyNotFoundException();
        }

        public override DestinationInfo GetHomeRegion(UUI user)
        {
            var hash = new Map
            {
                { "userID", user.ID.ToString() }
            };
            Map res = DoXmlRpcWithHashResponse("get_home_region", hash);
            if(!res["result"].AsBoolean)
            {
                throw new KeyNotFoundException();
            }

            var dInfo = new DestinationInfo()
            {
                /* assume that HomeURI supports Gatekeeper services */
                GatekeeperURI = user.HomeURI.ToString(),
                LocalToGrid = false,
                ID = res["uuid"].AsUUID
            };
            if (res.ContainsKey("x"))
            {
                dInfo.Location.X = res["x"].AsUInt;
            }
            if (res.ContainsKey("y"))
            {
                dInfo.Location.X = res["y"].AsUInt;
            }
            if (res.ContainsKey("size_x"))
            {
                dInfo.Size.X = res["size_x"].AsUInt;
            }
            else
            {
                dInfo.Size.GridX = 1;
            }
            if (res.ContainsKey("size_y"))
            {
                dInfo.Size.X = res["size_y"].AsUInt;
            }
            else
            {
                dInfo.Size.GridY = 1;
            }
            if(res.ContainsKey("region_name"))
            {
                dInfo.Name = res["region_name"].ToString();
            }
            if (res.ContainsKey("http_port"))
            {
                dInfo.ServerHttpPort = res["http_port"].AsUInt;
            }
            if (res.ContainsKey("internal_port"))
            {
                dInfo.ServerPort = res["internal_port"].AsUInt;
            }
            if (res.ContainsKey("hostname"))
            {
                IPAddress[] address = Dns.GetHostAddresses(res["hostname"].ToString());
                if (res.ContainsKey("internal_port") && address.Length > 0)
                {
                    dInfo.SimIP = new IPEndPoint(address[0], (int)dInfo.ServerPort);
                }
            }
            if (res.ContainsKey("server_uri"))
            {
                dInfo.ServerURI = res["server_uri"].ToString();
            }
            if(res.ContainsKey("position"))
            {
                dInfo.Position = Vector3.Parse(res["position"].ToString());
            }
            if (res.ContainsKey("lookAt"))
            {
                dInfo.LookAt = Vector3.Parse(res["lookAt"].ToString());
            }
            return dInfo;
        }

        public override bool IsOnline(UUI user) => false;

        private void DoXmlRpcWithBoolResponse(string method, Map reqparams)
        {
            var req = new XmlRpc.XmlRpcRequest(method);
            req.Params.Add(reqparams);
            XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);

            var hash = (Map)res.ReturnValue;
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

        private Map DoXmlRpcWithHashResponse(string method, Map reqparams)
        {
            var req = new XmlRpc.XmlRpcRequest(method);
            req.Params.Add(reqparams);
            XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);

            var hash = (Map)res.ReturnValue;
            if (hash == null)
            {
                throw new InvalidOperationException();
            }

            return hash;
        }

        bool IDisplayNameAccessor.TryGetValue(UUI agent, out string displayname)
        {
            displayname = string.Empty;
            return false;
        }

        bool IDisplayNameAccessor.ContainsKey(UUI agent) => false;
    }
}
