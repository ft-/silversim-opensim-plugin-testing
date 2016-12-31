// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.AuthInfo;
using SilverSim.Types;
using SilverSim.Types.AuthInfo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace SilverSim.BackendConnectors.Robust.AuthInfo
{
    [Serializable]
    public class RobustAuthInfoConnectorFailureException : Exception
    {
        public RobustAuthInfoConnectorFailureException()
        {

        }
        public RobustAuthInfoConnectorFailureException(string msg) : base(msg) { }
        public RobustAuthInfoConnectorFailureException(string msg, Exception innerException) : base(msg, innerException) { }
        protected RobustAuthInfoConnectorFailureException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    public class RobustAuthInfoConnector : AuthInfoServiceInterface, IPlugin
    {
        readonly string m_Uri;
        public int TimeoutMs { get; set; }

        public RobustAuthInfoConnector(string uri)
        {
            TimeoutMs = 20000;
            m_Uri = uri;
            if(!m_Uri.EndsWith("/"))
            {
                m_Uri += "/";
            }
            m_Uri += "auth/plain";
        }

        public override UserAuthInfo this[UUID accountid]
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override UUID AddToken(UUID principalId, UUID sessionid, int lifetime_in_minutes)
        {
            throw new NotSupportedException();
        }

        public override void ReleaseToken(UUID accountId, UUID secureSessionId)
        {
            Dictionary<string, string> postvals = new Dictionary<string, string>();
            postvals.Add("METHOD", "release");
            postvals.Add("PRINCIPALID", accountId.ToString());
            postvals.Add("TOKEN", secureSessionId.ToString());
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, postvals, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if(!map["Result"].AsBoolean)
            {
                throw new RobustAuthInfoConnectorFailureException();
            }
        }

        public override void ReleaseTokenBySession(UUID accountId, UUID sessionId)
        {
            throw new NotSupportedException();
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* intentionally left empty */
        }

        public override void Store(UserAuthInfo info)
        {
            throw new NotSupportedException();
        }

        public override void VerifyToken(UUID principalId, UUID token, int lifetime_extension_in_minutes)
        {
            Dictionary<string, string> postvals = new Dictionary<string, string>();
            postvals.Add("METHOD", "verify");
            postvals.Add("PRINCIPALID", principalId.ToString());
            postvals.Add("TOKEN", token.ToString());
            postvals.Add("LIFETIME", lifetime_extension_in_minutes.ToString());
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, postvals, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map["Result"].AsBoolean)
            {
                throw new RobustAuthInfoConnectorFailureException();
            }
        }

        public override UUID Authenticate(UUID sessionId, UUID principalId, string password, int lifetime_in_minutes)
        {
            Dictionary<string, string> postvals = new Dictionary<string, string>();
            postvals.Add("METHOD", "authenticate");
            postvals.Add("PRINCIPALID", principalId.ToString());
            postvals.Add("PASSWORD", password);
            postvals.Add("LIFETIME", lifetime_in_minutes.ToString());
            Map map;
            using (Stream s = HttpClient.DoStreamPostRequest(m_Uri, null, postvals, false, TimeoutMs))
            {
                map = OpenSimResponse.Deserialize(s);
            }
            if (!map["Result"].AsBoolean)
            {
                throw new RobustAuthInfoConnectorFailureException();
            }
            return map["Token"].AsUUID;
        }
    }

    #region Factory
    [PluginName("AuthInfo")]
    public class RobustAuthInfoConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST AUTHINFO CONNECTOR");
        public RobustAuthInfoConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            return new RobustAuthInfoConnector(
                ownSection.GetString("URI"));
        }
    }
    #endregion
}
