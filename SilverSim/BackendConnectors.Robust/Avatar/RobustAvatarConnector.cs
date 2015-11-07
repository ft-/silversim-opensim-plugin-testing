﻿// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Avatar;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization;
using System.Web;
using System.Xml;

namespace SilverSim.BackendConnectors.Robust.Avatar
{
    #region Service implementation
    [SuppressMessage("Gendarme.Rules.Exceptions", "DoNotThrowInUnexpectedLocationRule")]
    public sealed class RobustAvatarConnector : AvatarServiceInterface, IPlugin
    {
        [Serializable]
        public class AvatarInaccessibleException : Exception
        {
            public AvatarInaccessibleException()
            {

            }
            public AvatarInaccessibleException(string msg) : base(msg) {}
            public AvatarInaccessibleException(string msg, Exception innerException) : base(msg, innerException) { }
            protected AvatarInaccessibleException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }

        readonly string m_AvatarURI;
        public int TimeoutMs { get; set; }

        #region Constructor
        public RobustAvatarConnector(string uri)
        {
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
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["UserID"] = (string)avatarID;
                post["METHOD"] = "getavatar";
                post["VERSIONMIN"] = "0";
                post["VERSIONMAX"] = "1";
                Map map;
                using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
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
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["UserID"] = (string)avatarID;
                post["VERSIONMIN"] = "0";
                post["VERSIONMAX"] = "1";

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
                using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
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

                string outStr = "UserID=" + HttpUtility.UrlEncode((string)avatarID);
                outStr += "&METHOD=setitems";
                int i;
                for(i = 0; i < itemKeys.Count; ++i)
                {
                    outStr += "&";
                    outStr += HttpUtility.UrlEncode("Names[]") + "=" + HttpUtility.UrlEncode(itemKeys[i]);
                }
                for (i = 0; i < itemKeys.Count; ++i)
                {
                    outStr += "&";
                    outStr += HttpUtility.UrlEncode("Values[]") + "=" + HttpUtility.UrlEncode(value[i]);
                }
                outStr += "&VERSIONMIN=0&VERSIONMAX=0";

                Map map;
                using(Stream s = HttpRequestHandler.DoStreamRequest("POST", m_AvatarURI, null, "application/x-www-form-urlencoded", outStr, false, TimeoutMs))
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

        public override string this[UUID avatarID, string itemKey]
        {
            get
            {
                Dictionary<string, string> items = this[avatarID];
                return items[itemKey];
            }
            set
            {
                Dictionary<string, string> post = new Dictionary<string, string>();
                post["UserID"] = (string)avatarID;
                post["METHOD"] = "setitems";
                post["Names[]"] = itemKey;
                post["Values[]"] = value;
                post["VERSIONMIN"] = "0";
                post["VERSIONMAX"] = "1";
                Map map;
                using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)avatarID;
            post["METHOD"] = "removeitems";
            uint index = 0;
            foreach (string name in nameList)
            {
                post[String.Format("Names[]?{0}", index++)] = name.Replace(' ', '_');
            }
            post["VERSIONMIN"] = "0";
            post["VERSIONMAX"] = "1";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
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
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["UserID"] = (string)avatarID;
            post["METHOD"] = "removeitems";
            post["Names[]"] = name.Replace(' ', '_');
            post["VERSIONMIN"] = "0";
            post["VERSIONMAX"] = "1";
            Map map;
            using(Stream s = HttpRequestHandler.DoStreamPostRequest(m_AvatarURI, null, post, false, TimeoutMs))
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
    #endregion

    #region Factory
    [PluginName("Avatar")]
    public sealed class RobustAvatarConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST AVATAR CONNECTOR");
        public RobustAvatarConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustAvatarConnector(ownSection.GetString("URI"));
        }
    }
    #endregion
}
