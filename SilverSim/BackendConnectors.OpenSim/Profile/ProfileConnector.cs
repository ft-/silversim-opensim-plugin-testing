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
using SilverSim.ServiceInterfaces.Profile;
using System;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace SilverSim.BackendConnectors.OpenSim.Profile
{
    [Description("OpenSim Profile Connector")]
    public partial class ProfileConnector : ProfileServiceInterface, IPlugin
    {
        public interface IProfileConnectorImplementation : IClassifiedsInterface, IPicksInterface, INotesInterface, IUserPreferencesInterface, IPropertiesInterface
        {
        }

        [Serializable]
        public class RpcFaultException : Exception
        {
            public RpcFaultException()
            {
            }

            public RpcFaultException(string message)
                : base(message)
            {
            }

            protected RpcFaultException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public RpcFaultException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }

        internal IProfileConnectorImplementation m_ConnectorImplementation;
        public int TimeoutMs { get; set; }

        public ProfileConnector(string url)
        {
            TimeoutMs = 20000;
            m_ConnectorImplementation = new AutoDetectProfileConnector(this, url);
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public override IClassifiedsInterface Classifieds => m_ConnectorImplementation;

        public override IPicksInterface Picks => m_ConnectorImplementation;

        public override INotesInterface Notes => m_ConnectorImplementation;

        public override IUserPreferencesInterface Preferences => m_ConnectorImplementation;

        public override IPropertiesInterface Properties => m_ConnectorImplementation;
    }
}
