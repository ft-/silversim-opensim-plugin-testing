// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using SilverSim.Types;
using SilverSim.Types.Profile;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SilverSim.BackendConnectors.OpenSim.Profile
{
    public partial class ProfileConnector
    {
#if DEBUG
        static readonly ILog m_Log = LogManager.GetLogger("PROFILE AUTO-DETECT HANDLER");
#endif
        [Serializable]
        public class ProfileAutoDetectFailedException : Exception
        {
            public ProfileAutoDetectFailedException()
            {

            }

            public ProfileAutoDetectFailedException(string message)
                : base(message)
            {

            }

            protected ProfileAutoDetectFailedException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {

            }

            public ProfileAutoDetectFailedException(string message, Exception inner)
                : base(message, inner)
            {

            }
        }

        public class AutoDetectClassifiedsConnector : IClassifiedsInterface
        {
            readonly OpenSimClassifiedsConnector m_OpenSim;
            readonly RobustClassifiedsConnector m_Robust;
            readonly ProfileConnector m_Connector;
            
            public AutoDetectClassifiedsConnector(ProfileConnector connector, string url)
            {
                m_Connector = connector;
                m_OpenSim = new OpenSimClassifiedsConnector(connector, url);
                m_Robust = new RobustClassifiedsConnector(connector, url);
            }

            public Dictionary<UUID, string> GetClassifieds(UUI user)
            {
                Dictionary<UUID, string> res;
                try
                {
                    res = m_OpenSim.GetClassifieds(user);
                    m_Connector.m_Classifieds = m_OpenSim;
                    return res;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.getClassifieds: OpenSimProfile", e);
#endif
                }
                try
                {
                    res = m_Robust.GetClassifieds(user);
                    m_Connector.m_Classifieds = m_Robust;
                    return res;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.getClassifieds: CoreProfile", e);
#endif
                }
                throw new ProfileAutoDetectFailedException();
            }

            public ProfileClassified this[UUI user, UUID id]
            {
                get 
                {
                    ProfileClassified res;
                    try
                    {
                        res = m_OpenSim[user, id];
                        m_Connector.m_Classifieds = m_OpenSim;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Classifieds.this[UUI, UUID]: OpenSimProfile", e);
#endif
                    }
                    try
                    {
                        res = m_Robust[user, id];
                        m_Connector.m_Classifieds = m_Robust;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Classifieds.this[UUI, UUID]: CoreProfile", e);
#endif
                    }
                    throw new ProfileAutoDetectFailedException();
                }
            }


            public void Update(ProfileClassified classified)
            {
                try
                {
                    m_OpenSim.Update(classified);
                    m_Connector.m_Classifieds = m_OpenSim;
                    return;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.Update: OpenSimProfile", e);
#endif
                }
                try
                {
                    m_Robust.Update(classified);
                    m_Connector.m_Classifieds = m_Robust;
                    return;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.Update: CoreProfile", e);
#endif
                }
                throw new ProfileAutoDetectFailedException();
            }

            public void Delete(UUID id)
            {
                try
                {
                    m_OpenSim.Delete(id);
                    m_Connector.m_Classifieds = m_OpenSim;
                    return;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.Delete: OpenSimProfile", e);
#endif
                }
                try
                {
                    m_Robust.Delete(id);
                    m_Connector.m_Classifieds = m_Robust;
                    return;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.Delete: CoreProfile", e);
#endif
                }
                throw new ProfileAutoDetectFailedException();
            }
        }

        public class AutoDetectPicksConnector : IPicksInterface
        {
            readonly OpenSimPicksConnector m_OpenSim;
            readonly RobustPicksConnector m_Robust;
            readonly ProfileConnector m_Connector;

            public AutoDetectPicksConnector(ProfileConnector connector, string url)
            {
                m_Connector = connector;
                m_OpenSim = new OpenSimPicksConnector(connector, url);
                m_Robust = new RobustPicksConnector(connector, url);
            }

            public Dictionary<UUID, string> GetPicks(UUI user)
            {
                Dictionary<UUID, string> res;
                try
                {
                    res = m_OpenSim.GetPicks(user);
                    m_Connector.m_Picks = m_OpenSim;
                    return res;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.getPicks: OpenSimProfile", e);
#endif
                }
                try
                {
                    res = m_Robust.GetPicks(user);
                    m_Connector.m_Picks = m_Robust;
                    return res;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.getPicks: CoreProfile", e);
#endif
                }
                throw new ProfileAutoDetectFailedException();
            }

            public ProfilePick this[UUI user, UUID id]
            {
                get 
                {
                    ProfilePick res;
                    try
                    {
                        res = m_OpenSim[user, id];
                        m_Connector.m_Picks = m_OpenSim;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Picks.this[UUI, UUID]: OpenSimProfile", e);
#endif
                    }
                    try
                    {
                        res = m_Robust[user, id];
                        m_Connector.m_Picks = m_Robust;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Picks.this[UUI, UUID]: CoreProfile", e);
#endif
                    }
                    throw new ProfileAutoDetectFailedException();
                }
            }


            public void Update(ProfilePick pick)
            {
                try
                {
                    m_OpenSim.Update(pick);
                    m_Connector.m_Picks = m_OpenSim;
                    return;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.Update: OpenSimProfile", e);
#endif
                }
                try
                {
                    m_Robust.Update(pick);
                    m_Connector.m_Picks = m_Robust;
                    return;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.Update: CoreProfile", e);
#endif
                }
                throw new ProfileAutoDetectFailedException();
            }

            public void Delete(UUID id)
            {
                try
                {
                    m_OpenSim.Delete(id);
                    m_Connector.m_Picks = m_OpenSim;
                    return;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.Delete: OpenSimProfile", e);
#endif
                }
                try
                {
                    m_Robust.Delete(id);
                    m_Connector.m_Picks = m_Robust;
                    return;
                }
                catch
#if DEBUG
                    (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.Delete: CoreProfile", e);
#endif
                }
                throw new ProfileAutoDetectFailedException();
            }
        }

        public class AutoDetectNotesConnector : INotesInterface
        {
            readonly ProfileConnector m_Connector;
            readonly OpenSimNotesConnector m_OpenSim;
            readonly RobustNotesConnector m_Robust;

            public AutoDetectNotesConnector(ProfileConnector connector, string url)
            {
                m_Connector = connector;
                m_OpenSim = new OpenSimNotesConnector(connector, url);
                m_Robust = new RobustNotesConnector(connector, url);
            }

            public string this[UUI user, UUI target]
            {
                get
                {
                    string res;
                    try
                    {
                        res = m_OpenSim[user, target];
                        m_Connector.m_Notes = m_OpenSim;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Notes.this[UUI, UUI]: OpenSimProfile", e);
#endif
                    }
                    try
                    {
                        res = m_Robust[user, target];
                        m_Connector.m_Notes = m_Robust;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Notes.this[UUI, UUI]: CoreProfile", e);
#endif
                    }
                    throw new ProfileAutoDetectFailedException();
                }
                set
                {
                    try
                    {
                        m_OpenSim[user, target] = value;
                        m_Connector.m_Notes = m_OpenSim;
                        return;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Notes.this[UUI, UUI]: OpenSimProfile", e);
#endif
                    }
                    try
                    {
                        m_Robust[user, target] = value;
                        m_Connector.m_Notes = m_Robust;
                        return;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Notes.this[UUI, UUI]: CoreProfile", e);
#endif
                    }
                    throw new ProfileAutoDetectFailedException();
                }
            }
        }

        public class AutoDetectUserPreferencesConnector : IUserPreferencesInterface
        {
            readonly ProfileConnector m_Connector;
            readonly OpenSimUserPreferencesConnector m_OpenSim;
            readonly RobustUserPreferencesConnector m_Robust;

            public AutoDetectUserPreferencesConnector(ProfileConnector connector, string url)
            {
                m_Connector = connector;
                m_OpenSim = new OpenSimUserPreferencesConnector(connector, url);
                m_Robust = new RobustUserPreferencesConnector(connector, url);
            }

            public ProfilePreferences this[UUI user]
            {
                get
                {
                    ProfilePreferences res;
                    try
                    {
                        res = m_OpenSim[user];
                        m_Connector.m_Preferences = m_OpenSim;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Preferences.this[UUI, UUI]: OpenSimProfile", e);
#endif
                    }
                    try
                    {
                        res = m_Robust[user];
                        m_Connector.m_Preferences = m_Robust;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Preferences.this[UUI, UUI]: CoreProfile", e);
#endif
                    }
                    throw new ProfileAutoDetectFailedException();
                }
                set
                {
                    try
                    {
                        m_OpenSim[user] = value;
                        m_Connector.m_Preferences = m_OpenSim;
                        return;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Preferences.this[UUI, UUI]: OpenSimProfile", e);
#endif
                    }
                    try
                    {
                        m_Robust[user] = value;
                        m_Connector.m_Preferences = m_Robust;
                        return;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Preferences.this[UUI, UUI]: CoreProfile", e);
#endif
                    }
                    throw new ProfileAutoDetectFailedException();
                }
            }
        }

        public class AutoDetectPropertiesConnector : IPropertiesInterface
        {
            readonly ProfileConnector m_Connector;
            readonly OpenSimPropertiesConnector m_OpenSim;
            readonly RobustPropertiesConnector m_Robust;

            public AutoDetectPropertiesConnector(ProfileConnector connector, string url)
            {
                m_Connector = connector;
                m_OpenSim = new OpenSimPropertiesConnector(connector, url);
                m_Robust = new RobustPropertiesConnector(connector, url);
            }

            public ProfileProperties this[UUI user]
            {
                get
                {
                    ProfileProperties res;
                    try
                    {
                        res = m_OpenSim[user];
                        m_Connector.m_Properties = m_OpenSim;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Properties.this[UUI, UUI]: OpenSimProfile", e);
#endif
                    }
                    try
                    {
                        res = m_Robust[user];
                        m_Connector.m_Properties = m_Robust;
                        return res;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Properties.this[UUI, UUI]: CoreProfile", e);
#endif
                    }
                    throw new ProfileAutoDetectFailedException();
                }
            }
            public ProfileProperties this[UUI user, PropertiesUpdateFlags flags] 
            { 
                set
                {
                    try
                    {
                        m_OpenSim[user, flags] = value;
                        m_Connector.m_Properties = m_OpenSim;
                        return;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Properties.this[UUI, UUI]: OpenSimProfile", e);
#endif
                    }
                    try
                    {
                        m_Robust[user, flags] = value;
                        m_Connector.m_Properties = m_Robust;
                        return;
                    }
                    catch
#if DEBUG
                        (Exception e)
#endif
                    {
                        /* no action needed */
#if DEBUG
                        m_Log.Debug("Properties.this[UUI, UUI]: CoreProfile", e);
#endif
                    }
                    throw new ProfileAutoDetectFailedException();
                }
            }
        }
    }
}
