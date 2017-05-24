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
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Scene;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.Neighbor;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Grid;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Timers;

namespace SilverSim.Backend.OpenSim.Neighbor.Neighbor
{
    [Description("OpenSim Neighbor Connector")]
    [PluginName("OpenSimNeighbor")]
    public class OpenSimNeighbor : NeighborServiceInterface, IPlugin, IPluginShutdown, IPluginSubFactory
    {
        private readonly RwLockedDictionary<UUID, NeighborList> m_NeighborLists = new RwLockedDictionary<UUID, NeighborList>();
        private readonly object m_NeighborListAddLock = new object();
        private static OpenSimNeighborHandler m_NeighborHandler;
        private static readonly object m_NeighborHandlerInitLock = new object();
        private bool m_ShutdownRequestThread;
        private readonly System.Timers.Timer m_Timer = new System.Timers.Timer(3600000);

        private readonly BlockingQueue<UUID> m_NeighborNotifyRequestQueue = new BlockingQueue<UUID>();
        private SceneList m_Scenes;

        private static void InitNeighborHandler(ConfigurationLoader loader, OpenSimNeighbor neigh)
        {
            lock (m_NeighborHandlerInitLock)
            {
                if (m_NeighborHandler == null)
                {
                    m_NeighborHandler = new OpenSimNeighborHandler(neigh);
                    loader.AddPlugin("OpenSimNeighborHandler", m_NeighborHandler);
                }
            }
        }

        public void AddPlugins(ConfigurationLoader loader)
        {
            InitNeighborHandler(loader, this);
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Scenes = loader.Scenes;
            ThreadManager.CreateThread(RequestThread).Start();
            m_Timer.Elapsed += UpdateTimer;
            m_Timer.Start();
        }

        public void Shutdown()
        {
            m_ShutdownRequestThread = true;
            m_Timer.Stop();
            m_Timer.Elapsed -= UpdateTimer;
        }

        private void UpdateTimer(object sender, ElapsedEventArgs e)
        {
            foreach(UUID regionID in m_NeighborLists.Keys)
            {
                m_NeighborNotifyRequestQueue.Enqueue(regionID);
            }
        }

        private void RequestThread()
        {
            Thread.CurrentThread.Name = "OpenSim Neighbor Notify Thread";
            while (!m_ShutdownRequestThread)
            {
                UUID localRegionID;
                try
                {
                    localRegionID = m_NeighborNotifyRequestQueue.Dequeue(1000);
                }
                catch
                {
                    continue;
                }

                SceneInterface scene;
                if (!m_Scenes.TryGetValue(localRegionID, out scene))
                {
                    continue;
                }

                GridServiceInterface gridService = scene.GridService;
                RegionInfo regionInfo = scene.GetRegionInfo();
                if (gridService == null)
                {
                    continue;
                }

                List<RegionInfo> neighbors;
                try
                {
                    neighbors = NeighborRequester.GetNeighbors(gridService, regionInfo);
                }
                catch
                {
                    continue;
                }

                foreach (RegionInfo neighbor in neighbors)
                {
                    try
                    {
                        OpenSimNeighborConnector.NotifyNeighborStatus(regionInfo, neighbor);
                        m_NeighborLists[regionInfo.ID].Add(neighbor);
                        scene.NotifyNeighborOnline(neighbor);
                    }
                    catch
                    {
                        /* something failed so do not add neighbor */
                        m_NeighborLists[regionInfo.ID].Remove(neighbor);
                        scene.NotifyNeighborOffline(neighbor);
                    }
                }
            }
        }

        public ShutdownOrder ShutdownOrder => ShutdownOrder.LogoutRegion;

        public override void NotifyNeighborStatus(RegionInfo fromRegion)
        {
            if(m_Scenes.ContainsKey(fromRegion.ID))
            {
                if ((fromRegion.Flags & RegionFlags.RegionOnline) != 0)
                {
                    lock (m_NeighborListAddLock) /* only the following two must be made atomic */
                    {
                        if (!m_NeighborLists.ContainsKey(fromRegion.ID))
                        {
                            m_NeighborLists.Add(fromRegion.ID, new NeighborList());
                        }
                    }
                    m_NeighborNotifyRequestQueue.Enqueue(fromRegion.ID);
                }
                else
                {
                    /* OpenSim does not have a notify offline message */
                    m_NeighborLists.Remove(fromRegion.ID);
                }
            }
        }

        public void NotifyRemoteNeighborStatus(RegionInfo fromRegion, UUID toRegionID)
        {
            SceneInterface scene;
            if(m_Scenes.TryGetValue(toRegionID, out scene))
            {
                /* some matching request validate that it is really a neighbor and not some wannabe */
                RegionInfo rinfo;
                try
                {
                    rinfo = scene.GridService[fromRegion.ScopeID, fromRegion.ID];
                }
                catch(KeyNotFoundException)
                {
                    /* definitely not a neighbor */
                    return;
                }
                catch
                {
                    /* TODO: make a retry request here? */
                    return;
                }

                if(rinfo.ServerURI != fromRegion.ServerURI ||
                    rinfo.ServerIP != fromRegion.ServerIP ||
                    rinfo.ServerPort != fromRegion.ServerPort)
                {
                    /* this one cannot be a neighbor since it does not match with grid details */
                    return;
                }
            }
            else
            {
                return;
            }

            /* we got a valid region match, so we can actually consider it a neighbor */
            m_NeighborLists[toRegionID].Add(fromRegion);
            scene.NotifyNeighborOnline(fromRegion);
        }
    }
}
