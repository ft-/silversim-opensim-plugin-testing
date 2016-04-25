// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Scene.Management.IM;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.IM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Timers;

namespace SilverSim.BackendConnectors.Robust.IM
{
    #region Service Implementation
    [SuppressMessage("Gendarme.Rules.Design", "TypesWithDisposableFieldsShouldBeDisposableRule")]
    [Description("Robust HG IM Connector")]
    public class RobustHGIM : IPlugin, IPluginShutdown
    {
        readonly RwLockedDictionary<string, KeyValuePair<int, IM.RobustIMConnector>> m_IMUrlCache = new RwLockedDictionary<string, KeyValuePair<int, IM.RobustIMConnector>>();

        public int TimeoutMs { get; set; }
        readonly List<AvatarNameServiceInterface> m_AvatarNameServices = new List<AvatarNameServiceInterface>();
        readonly List<string> m_AvatarNameServiceNames;
        PresenceServiceInterface m_PresenceService;
        readonly string m_PresenceServiceName;
        readonly Timer m_Timer;
        IMRouter m_IMRouter;

        public RobustHGIM(List<string> avatarNameServiceNames, string presenceServiceName)
        {
            TimeoutMs = 20000;
            m_AvatarNameServiceNames = avatarNameServiceNames;
            m_PresenceServiceName = presenceServiceName;
            m_Timer = new Timer(60000);
            m_Timer.Elapsed += CleanupTimer;
            m_Timer.Start();
        }

        void CleanupTimer(object sender, ElapsedEventArgs e)
        {
            List<string> removeList = new List<string>();
            foreach(KeyValuePair<string, KeyValuePair<int, IM.RobustIMConnector>> kvp in m_IMUrlCache)
            {
                if(Environment.TickCount - kvp.Value.Key > 60000)
                {
                    removeList.Add(kvp.Key);
                }
            }

            foreach(string url in removeList)
            {
                m_IMUrlCache.Remove(url);
            }
        }

        public bool Send(GridInstantMessage im)
        {
            UUI resolved = im.ToAgent;
            bool isResolved = false;

            foreach(AvatarNameServiceInterface avatarNameService in m_AvatarNameServices)
            {
                try
                {
                    resolved = avatarNameService[resolved];
                    isResolved = true;
                    break;
                }
                catch
                {
                    /* no action required */
                }
            }

            if(!isResolved)
            {
                return false;
            }

            /*
            List<PresenceInfo> presences;
            try
            {
                presences = m_PresenceService[resolved.ID];
            }
            catch
            {
                presences = new List<PresenceInfo>();
            }

            foreach(PresenceInfo pi in presences)
            {

            }
            */

            KeyValuePair<int, RobustIMConnector> kvp;
            RobustIMConnector imservice;
            if (!m_IMUrlCache.TryGetValue(resolved.HomeURI.ToString(), out kvp))
            {
                string imurl;
                Dictionary<string, string> urls;
                try
                {
                    urls = new UserAgent.RobustUserAgentConnector(resolved.HomeURI.ToString()).GetServerURLs(resolved);
                }
                catch
                {
                    return false;
                }

                if (!urls.TryGetValue("IMServerURI", out imurl))
                {
                    return false;
                }
                imservice = new IM.RobustIMConnector(imurl);
                m_IMUrlCache[resolved.HomeURI.ToString()] = new KeyValuePair<int, IM.RobustIMConnector>(Environment.TickCount, imservice);
            }
            else
            {
                imservice = kvp.Value;
            }

            try
            {
                imservice.Send(im);
                return true;
            }
            catch
            {
                return false;
            }
        }
    
        public void Startup(ConfigurationLoader loader)
        {
            m_IMRouter = loader.IMRouter;
            foreach (string servicename in m_AvatarNameServiceNames)
            {
                m_AvatarNameServices.Add(loader.GetService<AvatarNameServiceInterface>(servicename));
            }
            m_PresenceService = loader.GetService<PresenceServiceInterface>(m_PresenceServiceName);
            m_IMRouter.GridIM.Add(Send);
        }

        public ShutdownOrder ShutdownOrder
        {
            get 
            {
                return ShutdownOrder.LogoutRegion;
            }
        }

        public void Shutdown()
        {
            if (null != m_IMRouter)
            {
                m_IMRouter.GridIM.Remove(Send);
            }
            m_Timer.Stop();
        }
    }
    #endregion

    #region Service Factory
    [PluginName("RobustHGIM")]
    public class RobustHGIMFactory : IPluginFactory
    {
        public RobustHGIMFactory()
        {

        }
    
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownConfig)
        {
            List<string> avatarNameServiceNames = new List<string>();
            string avatarNameServices = ownConfig.GetString("AvatarNameServices", string.Empty);
            string presenceServiceName = ownConfig.GetString("PresenceService", string.Empty);
            foreach(string p in avatarNameServices.Split(','))
            {
                avatarNameServiceNames.Add(p.Trim());
            }

            return new RobustHGIM(avatarNameServiceNames, presenceServiceName);
        }
    }
    #endregion
}
