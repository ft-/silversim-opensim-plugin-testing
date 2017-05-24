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
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Avatar;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Web;
using System.Xml;

namespace SilverSim.BackendConnectors.Robust.Avatar
{
    [Description("Robust Avatar Connector")]
    [PluginName("Avatar")]
    public sealed class RobustAvatarConnector : AvatarServiceInterface, IPlugin
    {
        [Serializable]
        public class AvatarInaccessibleException : Exception
        {
            public AvatarInaccessibleException()
            {
            }

            public AvatarInaccessibleException(string msg) : base(msg)
            {
            }

            public AvatarInaccessibleException(string msg, Exception innerException) : base(msg, innerException)
            {
            }

            protected AvatarInaccessibleException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST AVATAR CONNECTOR");

        private readonly string m_AvatarURI;
        public int TimeoutMs { get; set; }

        #region Constructor
        public RobustAvatarConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            string uri = ownSection.GetString("URI");
            TimeoutMs = 20000;
            if(!uri.EndsWith("/"))
            {
                uri += "/";
            }
            m_AvatarURI = uri + "avatar";
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }
        #endregion

        public override Dictionary<string, string> this[UUID avatarID]
        {
            get
            {
                var post = new Dictionary<string, string>
                {
                    ["UserID"] = (string)avatarID,
                    ["METHOD"] = "getavatar",
                    ["VERSIONMIN"] = "0",
                    ["VERSIONMAX"] = "1"
                };
                Map map;
                using(Stream s = HttpClient.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }
                if(!(map["result"] is Map))
                {
                    throw new AvatarInaccessibleException();
                }
                Map result = (Map)map["result"];
                if(result.Count == 0)
                {
                    return new Dictionary<string, string>();
                }
                Dictionary<string, string> data = new Dictionary<string, string>();
                foreach(KeyValuePair<string, IValue> kvp in result)
                {
                    string key = XmlConvert.DecodeName(kvp.Key);
                    data[key.Replace('_', ' ')] = kvp.Value.AsString.ToString();
                }
                return data;
            }
            set
            {
                var post = new Dictionary<string, string>
                {
                    ["UserID"] = (string)avatarID,
                    ["VERSIONMIN"] = "0",
                    ["VERSIONMAX"] = "1"
                };
                if (value != null)
                {
                    post["METHOD"] = "setavatar";
                    foreach (KeyValuePair<string, string> kvp in value)
                    {
                        string key = kvp.Key.Replace(' ', '_');
                        post[key] = kvp.Value;
                    }
                }
                else
                {
                    post["METHOD"] = "resetavatar";
                }
                Map map;
                using(Stream s = HttpClient.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }
                if(!map.ContainsKey("result"))
                {
                    throw new AvatarUpdateFailedException();
                }
                if(map["result"].ToString() != "Success")
                {
                    throw new AvatarUpdateFailedException();
                }
            }
        }

        public override List<string> this[UUID avatarID, IList<string> itemKeys]
        {
            get
            {
                Dictionary<string, string> res = this[avatarID];
                List<string> result = new List<string>();
                foreach (string key in itemKeys)
                {
                    string val;
                    result.Add( res.TryGetValue(key, out val) ? val : string.Empty);
                }
                return result;
            }
            set
            {
                if(value == null || itemKeys == null)
                {
                    throw new ArgumentNullException("value");
                }
                if(itemKeys.Count != value.Count)
                {
                    throw new ArgumentException("value and itemKeys must have identical Count");
                }

                StringBuilder outStr = new StringBuilder("UserID=" + HttpUtility.UrlEncode((string)avatarID));
                outStr.Append("&METHOD=setitems");
                int i;
                for(i = 0; i < itemKeys.Count; ++i)
                {
                    outStr.Append("&");
                    outStr.Append(HttpUtility.UrlEncode("Names[]"));
                    outStr.Append("=");
                    outStr.Append(HttpUtility.UrlEncode(itemKeys[i]));
                }
                for (i = 0; i < itemKeys.Count; ++i)
                {
                    outStr.Append("&");
                    outStr.Append(HttpUtility.UrlEncode("Values[]"));
                    outStr.Append("=");
                    outStr.Append(HttpUtility.UrlEncode(value[i]));
                }
                outStr.Append("&VERSIONMIN=0&VERSIONMAX=1");

                Map map;
                using(Stream s = HttpClient.DoStreamRequest("POST", m_AvatarURI, null, "application/x-www-form-urlencoded", outStr.ToString(), false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }
                if (!map.ContainsKey("result"))
                {
                    throw new AvatarUpdateFailedException();
                }
                if (map["result"].ToString() != "Success")
                {
                    throw new AvatarUpdateFailedException();
                }
            }
        }

        public override bool TryGetValue(UUID avatarID, string itemKey, out string value)
        {
            Dictionary<string, string> items = this[avatarID];
            return items.TryGetValue(itemKey, out value);
        }

        public override string this[UUID avatarID, string itemKey]
        {
            get
            {
                Dictionary<string, string> items = this[avatarID];
                return items[itemKey];
            }
            set
            {
                var post = new Dictionary<string, string>
                {
                    ["UserID"] = (string)avatarID,
                    ["METHOD"] = "setitems",
                    ["Names[]"] = itemKey,
                    ["Values[]"] = value,
                    ["VERSIONMIN"] = "0",
                    ["VERSIONMAX"] = "1"
                };
                Map map;
                using(Stream s = HttpClient.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
                {
                    map = OpenSimResponse.Deserialize(s);
                }
                if (!map.ContainsKey("result"))
                {
                    throw new AvatarUpdateFailedException();
                }
                if (map["result"].ToString() != "Success")
                {
                    throw new AvatarUpdateFailedException();
                }
            }
        }

        public override void Remove(UUID avatarID, IList<string> nameList)
        {
            var post = new Dictionary<string, string>
            {
                ["UserID"] = (string)avatarID,
                ["METHOD"] = "removeitems"
            };
            uint index = 0;
            foreach (string name in nameList)
            {
                post[String.Format("Names[]?{0}", index++)] = name.Replace(' ', '_');
            }
            post["VERSIONMIN"] = "0";
            post["VERSIONMAX"] = "1";
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("result"))
            {
                throw new AvatarUpdateFailedException();
            }
            if (map["result"].ToString() != "Success")
            {
                throw new AvatarUpdateFailedException();
            }
        }

        public override void Remove(UUID avatarID, string name)
        {
            var post = new Dictionary<string, string>
            {
                ["UserID"] = (string)avatarID,
                ["METHOD"] = "removeitems",
                ["Names[]"] = name.Replace(' ', '_'),
                ["VERSIONMIN"] = "0",
                ["VERSIONMAX"] = "1"
            };
            Map map;
            using(Stream s = HttpClient.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map.ContainsKey("result"))
            {
                throw new AvatarUpdateFailedException();
            }
            if (map["result"].ToString() != "Success")
            {
                throw new AvatarUpdateFailedException();
            }
        }
    }
}
