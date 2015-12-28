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

        internal IClassifiedsInterface m_Classifieds;
        internal IPicksInterface m_Picks;
        internal INotesInterface m_Notes;
        internal IUserPreferencesInterface m_Preferences;
        internal IPropertiesInterface m_Properties;
        public int TimeoutMs { get; set; }

        public ProfileConnector(string url)
        {
            TimeoutMs = 20000;
            m_Classifieds = new AutoDetectClassifiedsConnector(this, url);
            m_Picks = new AutoDetectPicksConnector(this, url);
            m_Notes = new AutoDetectNotesConnector(this, url);
            m_Preferences = new AutoDetectUserPreferencesConnector(this, url);
            m_Properties = new AutoDetectPropertiesConnector(this, url);
        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
        }

        public override ProfileServiceInterface.IClassifiedsInterface Classifieds
        {
            get 
            {
                return m_Classifieds; 
            }
        }

        public override ProfileServiceInterface.IPicksInterface Picks
        {
            get 
            {
                return m_Picks;
            }
        }

        public override ProfileServiceInterface.INotesInterface Notes
        {
            get
            {
                return m_Notes;
            }
        }

        public override ProfileServiceInterface.IUserPreferencesInterface Preferences
        {
            get 
            {
                return m_Preferences;
            }
        }

        public override ProfileServiceInterface.IPropertiesInterface Properties
        {
            get 
            {
                return m_Properties;
            }
        }
    }
}
