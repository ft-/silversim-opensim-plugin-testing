﻿// SilverSim is distributed under the terms of the
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

        public class AutoDetectProfileConnector : IProfileConnectorImplementation
        {
            readonly OpenSimProfileConnector m_OpenSim;
            readonly RobustProfileConnector m_Robust;
            readonly ProfileConnector m_Connector;
            
            public AutoDetectProfileConnector(ProfileConnector connector, string url)
            {
                m_Connector = connector;
                m_OpenSim = new OpenSimProfileConnector(connector, url);
                m_Robust = new RobustProfileConnector(connector, url);
            }

            Dictionary<UUID, string> IClassifiedsInterface.GetClassifieds(UUI user)
            {
                Dictionary<UUID, string> res;
                try
                {
                    res = ((IClassifiedsInterface)m_OpenSim).GetClassifieds(user);
                    m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                    res = ((IClassifiedsInterface)m_Robust).GetClassifieds(user);
                    m_Connector.m_ConnectorImplementation = m_Robust;
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

            bool IClassifiedsInterface.TryGetValue(UUI user, UUID id, out ProfileClassified classified)
            {
                try
                {
                    if (((IClassifiedsInterface)m_OpenSim).TryGetValue(user, id, out classified))
                    {
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.TryGetValue(UUI, UUID, out ProfileClassified): OpenSimProfile", e);
#endif
                }
                try
                {
                    if (((IClassifiedsInterface)m_Robust).TryGetValue(user, id, out classified))
                    {
                        m_Connector.m_ConnectorImplementation = m_Robust;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.TryGetValue(UUI, UUID, out ProfileClassified): CoreProfile", e);
#endif
                }
                classified = default(ProfileClassified);
                return false;
            }

            bool IClassifiedsInterface.ContainsKey(UUI user, UUID id)
            {
                try
                {
                    if (((IClassifiedsInterface)m_OpenSim).ContainsKey(user, id))
                    {
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.ContainsKey(UUI, UUID): OpenSimProfile", e);
#endif
                }
                try
                {
                    if (((IClassifiedsInterface)m_Robust).ContainsKey(user, id))
                    {
                        m_Connector.m_ConnectorImplementation = m_Robust;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Classifieds.ContainsKey(UUI, UUID): CoreProfile", e);
#endif
                }
                return false;
            }

            ProfileClassified IClassifiedsInterface.this[UUI user, UUID id]
            {
                get 
                {
                    ProfileClassified res;
                    try
                    {
                        res = ((IClassifiedsInterface)m_OpenSim)[user, id];
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                        res = ((IClassifiedsInterface)m_Robust)[user, id];
                        m_Connector.m_ConnectorImplementation = m_Robust;
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


            void IClassifiedsInterface.Update(ProfileClassified classified)
            {
                try
                {
                    ((IClassifiedsInterface)m_OpenSim).Update(classified);
                    m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                    ((IClassifiedsInterface)m_Robust).Update(classified);
                    m_Connector.m_ConnectorImplementation = m_Robust;
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

            void IClassifiedsInterface.Delete(UUID id)
            {
                try
                {
                    ((IClassifiedsInterface)m_OpenSim).Delete(id);
                    m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                    ((IClassifiedsInterface)m_Robust).Delete(id);
                    m_Connector.m_ConnectorImplementation = m_Robust;
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

            Dictionary<UUID, string> IPicksInterface.GetPicks(UUI user)
            {
                Dictionary<UUID, string> res;
                try
                {
                    res = ((IPicksInterface)m_OpenSim).GetPicks(user);
                    m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                    res = ((IPicksInterface)m_Robust).GetPicks(user);
                    m_Connector.m_ConnectorImplementation = m_Robust;
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

            bool IPicksInterface.TryGetValue(UUI user, UUID id, out ProfilePick pick)
            {
                try
                {
                    if (((IPicksInterface)m_OpenSim).TryGetValue(user, id, out pick))
                    {
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.TryGetValue(UUI, UUID, out ProfilePick): OpenSimProfile", e);
#endif
                }
                try
                {
                    if (((IPicksInterface)m_Robust).TryGetValue(user, id, out pick))
                    {
                        m_Connector.m_ConnectorImplementation = m_Robust;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.TryGetValue(UUI, UUID, out ProfilePick): CoreProfile", e);
#endif
                }

                pick = default(ProfilePick);
                return false;
            }

            bool IPicksInterface.ContainsKey(UUI user, UUID id)
            {
                try
                {
                    if (((IPicksInterface)m_OpenSim).ContainsKey(user, id))
                    {
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.ContainsKey(UUI, UUID): OpenSimProfile", e);
#endif
                }
                try
                {
                    if (((IPicksInterface)m_Robust).ContainsKey(user, id))
                    {
                        m_Connector.m_ConnectorImplementation = m_Robust;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Picks.ContainsKey(UUI, UUID): CoreProfile", e);
#endif
                }

                return false;
            }

            ProfilePick IPicksInterface.this[UUI user, UUID id]
            {
                get 
                {
                    ProfilePick res;
                    try
                    {
                        res = ((IPicksInterface)m_OpenSim)[user, id];
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                        res = ((IPicksInterface)m_Robust)[user, id];
                        m_Connector.m_ConnectorImplementation = m_Robust;
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


            void IPicksInterface.Update(ProfilePick pick)
            {
                try
                {
                    ((IPicksInterface)m_OpenSim).Update(pick);
                    m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                    ((IPicksInterface)m_Robust).Update(pick);
                    m_Connector.m_ConnectorImplementation = m_Robust;
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

            void IPicksInterface.Delete(UUID id)
            {
                try
                {
                    ((IPicksInterface)m_OpenSim).Delete(id);
                    m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                    ((IPicksInterface)m_Robust).Delete(id);
                    m_Connector.m_ConnectorImplementation = m_Robust;
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

            bool INotesInterface.TryGetValue(UUI user, UUI target, out string notes)
            {
                try
                {
                    if (((INotesInterface)m_OpenSim).TryGetValue(user, target, out notes))
                    {
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Notes.TryGetValue(UUI, UUI, out string): OpenSimProfile", e);
#endif
                }
                try
                {
                    if (((INotesInterface)m_Robust).TryGetValue(user, target, out notes))
                    {
                        m_Connector.m_ConnectorImplementation = m_Robust;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Notes.TryGetValue(UUI, UUI, out string): CoreProfile", e);
#endif
                }

                notes = string.Empty;
                return false;
            }

            bool INotesInterface.ContainsKey(UUI user, UUI target)
            {
                try
                {
                    if (((INotesInterface)m_OpenSim).ContainsKey(user, target))
                    {
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Notes.ContainsKey(UUI, UUI): OpenSimProfile", e);
#endif
                }
                try
                {
                    if (((INotesInterface)m_Robust).ContainsKey(user, target))
                    {
                        m_Connector.m_ConnectorImplementation = m_Robust;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Notes.ContainsKey(UUI, UUI): CoreProfile", e);
#endif
                }

                return false;
            }

            string INotesInterface.this[UUI user, UUI target]
            {
                get
                {
                    string res;
                    try
                    {
                        res = ((INotesInterface)m_OpenSim)[user, target];
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                        res = ((INotesInterface)m_Robust)[user, target];
                        m_Connector.m_ConnectorImplementation = m_Robust;
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
                        ((INotesInterface)m_OpenSim)[user, target] = value;
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                        ((INotesInterface)m_Robust)[user, target] = value;
                        m_Connector.m_ConnectorImplementation = m_Robust;
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

            bool IUserPreferencesInterface.TryGetValue(UUI user, out ProfilePreferences prefs)
            {
                try
                {
                    if (((IUserPreferencesInterface)m_OpenSim).TryGetValue(user, out prefs))
                    {
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Preferences.TryGetValue(UUI, UUI, out ProfilePreferences): OpenSimProfile", e);
#endif
                }
                try
                {
                    if (((IUserPreferencesInterface)m_Robust).TryGetValue(user, out prefs))
                    {
                        m_Connector.m_ConnectorImplementation = m_Robust;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Preferences.TryGetValue(UUI, UUI, out ProfilePreferences): CoreProfile", e);
#endif
                }

                prefs = default(ProfilePreferences);
                return false;
            }

            bool IUserPreferencesInterface.ContainsKey(UUI user)
            {
                try
                {
                    if (((IUserPreferencesInterface)m_OpenSim).ContainsKey(user))
                    {
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Preferences.ContainsKey(UUI, UUI, out ProfilePreferences): OpenSimProfile", e);
#endif
                }
                try
                {
                    if (((IUserPreferencesInterface)m_Robust).ContainsKey(user))
                    {
                        m_Connector.m_ConnectorImplementation = m_Robust;
                        return true;
                    }
                }
                catch
#if DEBUG
                        (Exception e)
#endif
                {
                    /* no action needed */
#if DEBUG
                    m_Log.Debug("Preferences.ContainsKey(UUI, UUI, out ProfilePreferences): CoreProfile", e);
#endif
                }

                return false;
            }

            ProfilePreferences IUserPreferencesInterface.this[UUI user]
            {
                get
                {
                    ProfilePreferences res;
                    try
                    {
                        res = ((IUserPreferencesInterface)m_OpenSim)[user];
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                        res = ((IUserPreferencesInterface)m_Robust)[user];
                        m_Connector.m_ConnectorImplementation = m_Robust;
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
                        ((IUserPreferencesInterface)m_OpenSim)[user] = value;
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                        ((IUserPreferencesInterface)m_Robust)[user] = value;
                        m_Connector.m_ConnectorImplementation = m_Robust;
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

            ProfileProperties IPropertiesInterface.this[UUI user]
            {
                get
                {
                    ProfileProperties res;
                    try
                    {
                        res = ((IPropertiesInterface)m_OpenSim)[user];
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                        res = ((IPropertiesInterface)m_Robust)[user];
                        m_Connector.m_ConnectorImplementation = m_Robust;
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

            ProfileProperties IPropertiesInterface.this[UUI user, PropertiesUpdateFlags flags] 
            { 
                set
                {
                    try
                    {
                        ((IPropertiesInterface)m_OpenSim)[user, flags] = value;
                        m_Connector.m_ConnectorImplementation = m_OpenSim;
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
                        ((IPropertiesInterface)m_Robust)[user, flags] = value;
                        m_Connector.m_ConnectorImplementation = m_Robust;
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
