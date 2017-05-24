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
using SilverSim.ServiceInterfaces.AuthInfo;
using SilverSim.Types;
using SilverSim.Types.AuthInfo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public RobustAuthInfoConnectorFailureException(string msg) : base(msg)
        {
        }

        public RobustAuthInfoConnectorFailureException(string msg, Exception innerException) : base(msg, innerException)
        {
        }

        protected RobustAuthInfoConnectorFailureException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Description("Robust AuthInfo Connector")]
    [PluginName("AuthInfo")]
    public class RobustAuthInfoConnector : AuthInfoServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST AUTHINFO CONNECTOR");
        private readonly string m_Uri;
        public int TimeoutMs { get; set; }

        public RobustAuthInfoConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            string uri = ownSection.GetString("URI");

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
            get { throw new NotSupportedException(); }
        }

        public override UUID AddToken(UUID principalId, UUID sessionid, int lifetime_in_minutes)
        {
            throw new NotSupportedException();
        }

        public override void ReleaseToken(UUID accountId, UUID secureSessionId)
        {
            var postvals = new Dictionary<string, string>
            {
                ["METHOD"] = "release",
                ["PRINCIPALID"] = accountId.ToString(),
                ["TOKEN"] = secureSessionId.ToString()
            };
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
            var postvals = new Dictionary<string, string>
            {
                ["METHOD"] = "verify",
                ["PRINCIPALID"] = principalId.ToString(),
                ["TOKEN"] = token.ToString(),
                ["LIFETIME"] = lifetime_extension_in_minutes.ToString()
            };
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
            var postvals = new Dictionary<string, string>
            {
                ["METHOD"] = "authenticate",
                ["PRINCIPALID"] = principalId.ToString(),
                ["PASSWORD"] = password,
                ["LIFETIME"] = lifetime_in_minutes.ToString()
            };
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
}
