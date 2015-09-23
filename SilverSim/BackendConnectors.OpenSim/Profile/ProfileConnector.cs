// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.ServiceInterfaces.Profile;
using System;

namespace SilverSim.BackendConnectors.OpenSim.Profile
{
    public partial class ProfileConnector : ProfileServiceInterface
    {
        public class RpcFaultException : Exception
        {
            public RpcFaultException()
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
