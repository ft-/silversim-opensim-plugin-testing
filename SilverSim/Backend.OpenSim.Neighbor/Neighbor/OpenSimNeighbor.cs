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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Timers;
using ThreadedClasses;

namespace SilverSim.Backend.OpenSim.Neighbor.Neighbor
{
    [SuppressMessage("Gendarme.Rules.Design", "TypesWithDisposableFieldsShouldBeDisposableRule")]
    [SuppressMessage("Gendarme.Rules.Concurrency", "WriteStaticFieldFromInstanceMethodRule")]
    [Description("OpenSim Neighbor Connector")]
    public class OpenSimNeighbor : NeighborServiceInterface, IPlugin, IPluginShutdown, IPluginSubFactory
    {
        readonly RwLockedDictionary<UUID, NeighborList> m_NeighborLists = new RwLockedDictionary<UUID, NeighborList>();
        readonly object m_NeighborListAddLock = new object();
        static OpenSimNeighborHandler m_NeighborHandler;
        static object m_NeighborHandlerInitLock = new object();
        bool m_ShutdownRequestThread;
        readonly System.Timers.Timer m_Timer = new System.Timers.Timer(3600000);

        readonly BlockingQueue<UUID> m_NeighborNotifyRequestQueue = new BlockingQueue<UUID>();

        public OpenSimNeighbor()
        {
        }

        static void InitNeighborHandler(ConfigurationLoader loader, OpenSimNeighbor neigh)
        {
            lock (m_NeighborHandlerInitLock)
            {
                if (null == m_NeighborHandler)
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
            new Thread(RequestThread).Start();
            m_Timer.Elapsed += UpdateTimer;
            m_Timer.Start();
        }

        public void Shutdown()
        {
            m_ShutdownRequestThread = true;
            m_Timer.Stop();
            m_Timer.Elapsed -= UpdateTimer;
        }

        void UpdateTimer(object sender, ElapsedEventArgs e)
        {
            foreach(UUID regionID in m_NeighborLists.Keys)
            {
                m_NeighborNotifyRequestQueue.Enqueue(regionID);
            }
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

        public ShutdownOrder ShutdownOrder
        {
            get 
            {
                return ShutdownOrder.LogoutRegion;
            }
        }

        public override void NotifyNeighborStatus(RegionInfo fromRegion)
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

        public void NotifyRemoteNeighborStatus(RegionInfo fromRegion, UUID toRegionID)
        {
            SceneInterface scene;
            if(SceneManager.Scenes.TryGetValue(toRegionID, out scene))
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

    [PluginName("OpenSimNeighbor")]
    public class OpenSimNeighborFactory : IPluginFactory
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
