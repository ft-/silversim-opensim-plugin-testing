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
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Types;
using SilverSim.Types.Grid;
using SilverSim.Types.Presence;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendHandlers.Robust.Grid
{
    [PluginName("GridFriendsStatusNotifier")]
    public sealed class GridFriendsStatusNotifyService : IFriendsStatusNotifyServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("FRIENDSSTATUSNOTIFY");
        private readonly string m_GridServiceName;
        private GridServiceInterface m_GridService;

        private readonly string m_PresenceServiceName;
        private PresenceServiceInterface m_PresenceService;

        public GridFriendsStatusNotifyService(IConfig config)
        {
            m_GridServiceName = config.GetString("GridService", "RegionStorage");
            m_PresenceServiceName = config.GetString("PresenceService", "PresenceService");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_GridService = loader.GetService<GridServiceInterface>(m_GridServiceName);
            m_PresenceService = loader.GetService<PresenceServiceInterface>(m_PresenceServiceName);
        }

        public void NotifyAsOffline(UUI notifier, List<KeyValuePair<UUI, string>> list) => SendNotification(notifier, list, false);

        public void NotifyAsOnline(UUI notifier, List<KeyValuePair<UUI, string>> list) => SendNotification(notifier, list, true);

        private void SendNotification(UUI notifier, List<KeyValuePair<UUI, string>> list, bool isOnline)
        {
            var notifytargets = new Dictionary<UUID, List<UUI>>();
            foreach (KeyValuePair<UUI, string> id in list)
            {
                foreach (PresenceInfo pinfo in m_PresenceService[id.Key.ID])
                {
                    List<UUI> notified;
                    if (!notifytargets.TryGetValue(pinfo.RegionID, out notified))
                    {
                        notified = new List<UUI>();
                        notifytargets.Add(pinfo.RegionID, notified);
                    }
                }
            }

            foreach(KeyValuePair<UUID, List<UUI>> kvp in notifytargets)
            {
                try
                {
                    SendActualNotification(kvp.Key, notifier, kvp.Value, isOnline);
                }
                catch(Exception e)
                {
                    m_Log.Warn("Exception at notification", e);
                }
            }
        }

        private void SendActualNotification(UUID regionid, UUI notifier, List<UUI> list, bool isOnline)
        {
            RegionInfo rinfo;
            if(m_GridService.TryGetValue(regionid, out rinfo))
            {
                string uri = rinfo.ServerURI;
                if(!uri.EndsWith(uri))
                {
                    uri += "/";
                }
                uri += "friends";

                foreach (UUI id in list)
                {
                    var para = new Dictionary<string, string>
                    {
                        { "METHOD", "status" },
                        { "FromID", notifier.ID.ToString() },
                        { "ToID", id.ToString() },
                        { "Online", isOnline.ToString() }
                    };

                    new HttpClient.Post(uri, para).ExecuteRequest();
                }
            }
        }
    }
}
