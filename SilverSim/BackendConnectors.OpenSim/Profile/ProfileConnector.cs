// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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

        public override ProfileServiceInterface.IClassifiedsInterface Classifieds
        {
            get 
            {
                return m_ConnectorImplementation; 
            }
        }

        public override ProfileServiceInterface.IPicksInterface Picks
        {
            get 
            {
                return m_ConnectorImplementation;
            }
        }

        public override ProfileServiceInterface.INotesInterface Notes
        {
            get
            {
                return m_ConnectorImplementation;
            }
        }

        public override ProfileServiceInterface.IUserPreferencesInterface Preferences
        {
            get 
            {
                return m_ConnectorImplementation;
            }
        }

        public override ProfileServiceInterface.IPropertiesInterface Properties
        {
            get 
            {
                return m_ConnectorImplementation;
            }
        }
    }
}
