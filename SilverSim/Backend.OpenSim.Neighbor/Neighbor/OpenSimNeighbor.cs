// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Scene;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.Neighbor;
using SilverSim.Types;
using SilverSim.Types.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ThreadedClasses;

namespace SilverSim.Backend.OpenSim.Neighbor.Neighbor
{
    public class OpenSimNeighbor : NeighborServiceInterface, IPlugin, IPluginShutdown, IPluginSubFactory
    {
        RwLockedDictionary<UUID, NeighborList> m_NeighborLists = new RwLockedDictionary<UUID, NeighborList>();
        object m_NeighborListAddLock = new object();
        static OpenSimNeighbor m_NeighborHandler = null;
        static object m_NeighborHandlerInitLock = new object();
        bool m_ShutdownRequestThread = false;

        BlockingQueue<UUID> m_NeighborNotifyRequestQueue = new BlockingQueue<UUID>();

        public OpenSimNeighbor()
        {
        }

        public void AddPlugins(ConfigurationLoader loader)
        {
            lock (m_NeighborHandlerInitLock)
            {
                if (null == m_NeighborHandler)
                {
                    loader.AddPlugin("OpenSimNeighborHandler", m_NeighborHandler = new OpenSimNeighbor());
                }
            }
        }

        public void Startup(ConfigurationLoader loader)
        {
            new Thread(RequestThread).Start();
        }

        public void Shutdown()
        {
            m_ShutdownRequestThread = true;
        }

        void RequestThread()
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
                if (!SceneManager.Scenes.TryGetValue(localRegionID, out scene))
                {
                    continue;
                }

                GridServiceInterface gridService = scene.GridService;
                RegionInfo regionInfo = scene.RegionData;
                if (null == gridService)
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
                        OpenSimNeighborConnector.notifyNeighborStatus(regionInfo, neighbor);
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

        public ShutdownOrder ShutdownOrder
        {
            get 
            {
                return ShutdownOrder.LogoutRegion;
            }
        }

        public override void notifyNeighborStatus(RegionInfo fromRegion)
        {
            if(SceneManager.Scenes.ContainsKey(fromRegion.ID))
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

        public void notifyRemoteNeighborStatus(RegionInfo fromRegion, UUID toRegionID)
        {
            SceneInterface scene;
            if(SceneManager.Scenes.TryGetValue(toRegionID, out scene))
            {
                /* some matching request validate that it is really a neighbor and not some wannabe */
                try
                {
                    RegionInfo rinfo = scene.GridService[fromRegion.ScopeID, fromRegion.ID];
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

    [PluginName("OpenSimNeighbor")]
    class OpenSimNeighborFactory : IPluginFactory
    {
        public OpenSimNeighborFactory()
        {

        }
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new OpenSimNeighbor();
        }
    }
}
