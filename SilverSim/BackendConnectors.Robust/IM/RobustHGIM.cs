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
using System.Timers;

namespace SilverSim.BackendConnectors.Robust.IM
{
    [Description("Robust HG IM Connector")]
    [PluginName("RobustHGIM")]
    public class RobustHGIM : IPlugin, IPluginShutdown
    {
        private readonly RwLockedDictionary<string, KeyValuePair<int, IM.RobustIMConnector>> m_IMUrlCache = new RwLockedDictionary<string, KeyValuePair<int, IM.RobustIMConnector>>();

        public int TimeoutMs { get; set; }
        private readonly List<AvatarNameServiceInterface> m_AvatarNameServices = new List<AvatarNameServiceInterface>();
        private readonly List<string> m_AvatarNameServiceNames;
        private PresenceServiceInterface m_PresenceService;
        private readonly string m_PresenceServiceName;
        private readonly Timer m_Timer;
        private IMRouter m_IMRouter;

        public RobustHGIM(IConfig ownConfig)
        {
            var avatarNameServiceNames = new List<string>();
            string avatarNameServices = ownConfig.GetString("AvatarNameServices", string.Empty);
            string presenceServiceName = ownConfig.GetString("PresenceService", string.Empty);
            foreach (string p in avatarNameServices.Split(','))
            {
                avatarNameServiceNames.Add(p.Trim());
            }

            TimeoutMs = 20000;
            m_AvatarNameServiceNames = avatarNameServiceNames;
            m_PresenceServiceName = presenceServiceName;
            m_Timer = new Timer(60000);
            m_Timer.Elapsed += CleanupTimer;
            m_Timer.Start();
        }

        private void CleanupTimer(object sender, ElapsedEventArgs e)
        {
            var removeList = new List<string>();
            foreach(KeyValuePair<string, KeyValuePair<int, RobustIMConnector>> kvp in m_IMUrlCache)
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
            UGUI resolved = im.ToAgent;
            bool isResolved = false;

            foreach(AvatarNameServiceInterface avatarNameService in m_AvatarNameServices)
            {
                if(avatarNameService.TryGetValue(im.ToAgent, out resolved))
                {
                    isResolved = true;
                    break;
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
                imservice = new RobustIMConnector(imurl);
                m_IMUrlCache[resolved.HomeURI.ToString()] = new KeyValuePair<int, RobustIMConnector>(Environment.TickCount, imservice);
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

        public ShutdownOrder ShutdownOrder => ShutdownOrder.LogoutRegion;

        public void Shutdown()
        {
            if (m_IMRouter != null)
            {
                m_IMRouter.GridIM.Remove(Send);
            }
            m_Timer.Stop();
        }
    }
}
