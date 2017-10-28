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
using SilverSim.BackendConnectors.Robust.Gatekeeper;
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.CmdIO;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Agent;
using SilverSim.Scene.Types.Neighbor;
using SilverSim.Scene.Types.Object;
using SilverSim.Scene.Types.Scene;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.Teleport;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Agent;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.Json;
using SilverSim.Viewer.Core;
using SilverSim.Viewer.Messages.Teleport;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;

namespace SilverSim.BackendConnectors.OpenSim.Teleport
{
    [Description("OpenSim Teleport Protocol")]
    public class OpenSimTeleportProtocol : TeleportHandlerServiceInterface, IPlugin
    {
        internal const int PROTOCOL_VERSION_MAJOR = 0;
        internal const int PROTOCOL_VERSION_MINOR = 6;

        protected static readonly ILog m_Log = LogManager.GetLogger("OPENSIM TELEPORT PROTOCOL");
        private static readonly Random m_RandomNumber = new Random();
        private static readonly object m_RandomNumberLock = new object();
        public int TimeoutMs = 30000;
        private Thread m_TeleportThread;
        private readonly object m_TeleportThreadLock = new object();
        protected CommandRegistry Commands { get; private set; }
        private Main.Common.Caps.CapsHttpRedirector m_CapsRedirector;
        private List<IProtocolExtender> m_PacketHandlerPlugins = new List<IProtocolExtender>();
        private SceneList m_Scenes;

        internal static uint NewCircuitCode
        {
            get
            {
                int rand;
                lock (m_RandomNumberLock)
                {
                    rand = m_RandomNumber.Next(Int32.MinValue, Int32.MaxValue);
                }
                return (uint)rand;
            }
        }

        [Serializable]
        public class TeleportFailedException : Exception
        {
            public TeleportFailedException()
            {
            }

            public TeleportFailedException(string message)
                : base(message)
            {
            }

            protected TeleportFailedException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public TeleportFailedException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }

        internal static string NewCapsURL(string serverURI, UUID uuid) => serverURI + "CAPS/" + uuid.ToString() + "0000/";

        public OpenSimTeleportProtocol()
        {
        }

        public OpenSimTeleportProtocol(
            CommandRegistry commandRegistry,
            Main.Common.Caps.CapsHttpRedirector capsRedirector,
            List<IProtocolExtender> packetHandlerPlugins,
            SceneList scenes)
        {
            Commands = commandRegistry;
            m_CapsRedirector = capsRedirector;
            m_PacketHandlerPlugins = packetHandlerPlugins;
            m_Scenes = scenes;
        }

        public void Startup(ConfigurationLoader loader)
        {
            Commands = loader.CommandRegistry;
            m_CapsRedirector = loader.GetService<Main.Common.Caps.CapsHttpRedirector>("CapsRedirector");
            m_PacketHandlerPlugins = loader.GetServicesByValue<IProtocolExtender>();
            m_Scenes = loader.Scenes;
        }

        public override GridType GridType => new GridType("opensim-robust");

        public override void Cancel()
        {
            lock (m_TeleportThreadLock)
            {
                if (m_TeleportThread != null)
                {
                    m_TeleportThread.Abort();
                }
            }
        }

        public override void CloseAgentOnRelease(UUID fromSceneID)
        {
        }

        public override void ReleaseAgent(UUID fromSceneID)
        {
        }

        internal static string BuildAgentUri(RegionInfo destinationRegion, IAgent agent, string extra = "")
        {
            return BuildAgentUri(destinationRegion, agent.ID, extra);
        }

        internal static string BuildAgentUri(RegionInfo destinationRegion, UUID agentID, string extra = "")
        {
            string agentURL = destinationRegion.ServerURI;

            if (!agentURL.EndsWith("/"))
            {
                agentURL += "/";
            }

            return agentURL + "agent/" + agentID.ToString() + "/" + destinationRegion.ID.ToString() + "/" + extra;
        }

        public override void ReleaseAgent(UUID fromSceneID, IAgent agent, RegionInfo regionInfo)
        {
            string uri = BuildAgentUri(regionInfo, agent, "release");
            new HttpClient.Delete(uri) { TimeoutMs = TimeoutMs }.ExecuteRequest();
            agent.ActiveChilds.Remove(regionInfo.ID);
        }

        private void PostAgent(
            UUID fromSceneID,
            IAgent agent,
            DestinationInfo destinationRegion,
            int maxAllowedWearables,
            out uint circuitCode,
            out string capsPath) =>
            PostAgent(fromSceneID, agent, destinationRegion, UUID.Random, maxAllowedWearables, out circuitCode, out capsPath);

        private void PostAgent(
            UUID fromSceneID,
            IAgent agent,
            DestinationInfo destinationRegion,
            UUID capsId,
            int maxAllowedWearables,
            out uint circuitCode,
            out string capsPath)
        {
            string agentUri = BuildAgentUri(destinationRegion, agent);
#if DEBUG
            m_Log.DebugFormat("Sending region POST to {0} for {1} ({2})", agentUri, agent.Owner.FullName, agent.Owner.ID);
#endif
            PostAgent(
                fromSceneID,
                agent,
                destinationRegion,
                capsId,
                maxAllowedWearables,
                agentUri,
                out circuitCode,
                out capsPath);
        }

        private void PostAgent(
            UUID fromSceneID,
            IAgent agent,
            DestinationInfo destinationRegion, 
            UUID capsId,
            int maxAllowedWearables,
            string agentURL,
            out uint circuitCode,
            out string capsPath)
        {
            var vagent = (ViewerAgent)agent;
            AgentCircuit acirc = vagent.Circuits[fromSceneID];

            var agentPostData = new PostData();

            var childInfo = new AgentChildInfo
            {
                DestinationInfo = destinationRegion,
                TeleportService = this
            };
            agent.ActiveChilds.Add(destinationRegion.ID, childInfo);

            agentPostData.Account = agent.UntrustedAccountInfo;

            agentPostData.Appearance = agent.Appearance;

            agentPostData.Circuit = new CircuitInfo
            {
                CircuitCode = acirc.CircuitCode,
                CapsPath = capsId.ToString(),
                IsChild = true
            };
            agentPostData.Client = agent.Client;

            agentPostData.Destination = destinationRegion;

            agentPostData.Session = agent.Session;

            byte[] uncompressed_postdata;
            using (var ms = new MemoryStream())
            {
                agentPostData.Serialize(ms, maxAllowedWearables);
                uncompressed_postdata = ms.ToArray();
            }

            Map result;
            byte[] compressed_postdata;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionMode.Compress))
                {
                    gz.Write(uncompressed_postdata, 0, uncompressed_postdata.Length);
                }
                compressed_postdata = ms.ToArray();
            }
            try
            {
#if DEBUG
                m_Log.DebugFormat("Sending preferred POST request to {0}: Length={1}", agentURL, compressed_postdata.Length);
#endif
                using (Stream o = new HttpClient.Post(agentURL, "application/json", compressed_postdata.Length, (Stream ws) =>
                    ws.Write(compressed_postdata, 0, compressed_postdata.Length))
                {
                    IsCompressed = true,
                    TimeoutMs = TimeoutMs
                }.ExecuteStreamRequest())
                {
                    result = (Map)Json.Deserialize(o);
                }
            }
            catch
#if DEBUG
                (Exception e2)
#endif
            {
#if DEBUG
                m_Log.Debug("Exception", e2);
#endif
                try
                {
#if DEBUG
                    m_Log.DebugFormat("Sending old compressed POST request to {0}: Length={1}", agentURL, compressed_postdata.Length);
#endif
                    using (Stream o = new HttpClient.Post(agentURL, "application/x-gzip", compressed_postdata.Length, (Stream ws) =>
                        ws.Write(compressed_postdata, 0, compressed_postdata.Length))
                    {
                        TimeoutMs = TimeoutMs,
                    }.ExecuteStreamRequest())
                    {
                        result = (Map)Json.Deserialize(o);
                    }
                }
                catch
#if DEBUG
                    (Exception e3)
#endif
                {
#if DEBUG
                    m_Log.Debug("Exception", e3);
#endif
                    try
                    {
#if DEBUG
                        m_Log.DebugFormat("Sending uncompressed POST request to {0}: Length={1}", agentURL, uncompressed_postdata.Length);
#endif
                        using (Stream o = new HttpClient.Post(agentURL, "application/json", uncompressed_postdata.Length, (Stream ws) =>
                            ws.Write(uncompressed_postdata, 0, uncompressed_postdata.Length))
                        {
                            TimeoutMs = TimeoutMs
                        }.ExecuteStreamRequest())
                        {
                            result = (Map)Json.Deserialize(o);
                        }
                    }
                    catch (Exception e)
                    {
#if DEBUG
                        m_Log.Debug("Exception", e);
#endif
                        /* connect failed */
                        agent.ActiveChilds.Remove(destinationRegion.ID);
                        throw new TeleportFailedException(e.Message);
                    }
                }
            }

            if (result.ContainsKey("success"))
            {
                if (!result["success"].AsBoolean)
                {
                    /* not authorized */
                    if (result.ContainsKey("reason"))
                    {
                        throw new TeleportFailedException(result["reason"].ToString());
                    }
                    else
                    {
                        throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "NotAuthorized", "Not authorized"));
                    }
                }
            }
            else if (result.ContainsKey("reason"))
            {
                if (result["reason"].ToString() != "authorized")
                {
                    /* not authorized */
                    throw new TeleportFailedException(result["reason"].ToString());
                }
            }
            else
            {
                /* not authorized */
                throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "NotAuthorized", "Not authorized"));
            }

            circuitCode = agentPostData.Circuit.CircuitCode;
            capsPath = NewCapsURL(destinationRegion.ServerURI, agentPostData.Circuit.CapsPath);
        }

        public override void EnableSimulator(UUID fromSceneID, IAgent agent, DestinationInfo destinationRegion)
        {
            uint circuitCode;
            string capsPath;
#if DEBUG
            m_Log.DebugFormat("Enabling simulator {0} ({1}) at {2} for {3}", destinationRegion.Name, destinationRegion.ID, destinationRegion.ServerURI, agent.Owner.FullName);
#endif
            try
            {
                PostAgent(fromSceneID, agent, destinationRegion, 15, out circuitCode, out capsPath);
            }
            catch
            {
                return;
            }

            /* this makes the viewer go for a login to a neighbor */
            agent.EnableSimulator(fromSceneID, circuitCode, capsPath, destinationRegion);
        }

        public override void DisableSimulator(UUID fromSceneID, IAgent agent, RegionInfo regionInfo)
        {
            string uri = BuildAgentUri(regionInfo, agent, "?auth=" + agent.Session.SessionID.ToString());
#if DEBUG
            m_Log.DebugFormat("Disabling simulator {0} ({1}) at {2}", regionInfo.Name, regionInfo.ID, uri);
#endif
            new HttpClient.Delete(uri) { TimeoutMs = TimeoutMs }.ExecuteRequest();
            agent.ActiveChilds.Remove(regionInfo.ID);
        }

        #region Teleport Initiators
        public override bool TeleportHome(SceneInterface sceneInterface, IAgent agent)
        {
            lock (m_TeleportThreadLock)
            {
                if (m_TeleportThread == null)
                {
                    m_Log.DebugFormat("Teleport home requested for {0}", agent.Owner.FullName);

                    m_TeleportThread = ThreadManager.CreateThread(() =>
                    {
                        try
                        {
                            DestinationInfo dInfo = agent.UserAgentService.GetHomeRegion(agent.Owner);
                            TeleportTo_Step2(sceneInterface, agent, dInfo, dInfo.Position, dInfo.LookAt, TeleportFlags.ViaHome);
                        }
                        catch (TeleportFailedException e)
                        {
                            m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                            agent.SendAlertMessage(e.Message, sceneInterface.ID);
                        }
                        catch (Exception e)
                        {
                            m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                            throw;
                        }
                        finally
                        {
                            m_TeleportThread = null;
                            agent.RemoveActiveTeleportService(this);
                        }
                    });
                    agent.ActiveTeleportService = this;
                    m_TeleportThread.Start();
                    return true;
                }
                else
                {
                    m_Log.DebugFormat("Teleport home requested for {0} not possible", agent.Owner.FullName);
                }
            }
            return false;
        }

        public override bool TeleportTo(SceneInterface sceneInterface, IAgent agent, string regionName, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            /* foreign grid */
            lock (m_TeleportThreadLock)
            {
                if (m_TeleportThread == null)
                {
                    m_Log.DebugFormat("Teleport to {0} requested for {1}: {2}", regionName, agent.Owner.FullName, flags.ToString());

                    m_TeleportThread = ThreadManager.CreateThread(() =>
                    {
                        try
                        {
                            DestinationInfo dInfo;
                            try
                            {
                                dInfo = TeleportTo_Step1_RegionNameLookup(sceneInterface, agent, regionName, flags);
                            }
                            catch (TeleportFailedException e)
                            {
                                m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                                agent.SendAlertMessage(e.Message, sceneInterface.ID);
                                return;
                            }
                            catch (Exception e)
                            {
                                m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                                throw;
                            }
                            try
                            {
                                TeleportTo_Step2(sceneInterface, agent, dInfo, position, lookAt, flags);
                            }
                            catch (Exception e)
                            {
                                m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                                TeleportFailed failedMsg = new TeleportFailed()
                                {
                                    AgentID = agent.ID,
                                    Reason = e.Message
                                };
                                agent.SendMessageIfRootAgent(failedMsg, sceneInterface.ID);
                            }
                        }
                        finally
                        {
                            m_TeleportThread = null;
                            agent.RemoveActiveTeleportService(this);
                        }
                    });
                    agent.ActiveTeleportService = this;
                    m_TeleportThread.Start();
                    return true;
                }
                else
                {
                    m_Log.DebugFormat("Teleport to {0} requested for {1} not possible: {2}", regionName, agent.Owner.FullName, flags.ToString());
                }
            }
            return false;
        }

        public override bool TeleportTo(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, GridVector location, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            bool isSameGrid = gatekeeperURI == sceneInterface.GatekeeperURI;

            lock (m_TeleportThreadLock)
            {
                if (m_TeleportThread == null)
                {
                    m_Log.DebugFormat("Teleport to {4} grid at {0},{1} requested for {2}: {3}", location.GridX, location.GridY, agent.Owner.FullName, flags.ToString(), isSameGrid ? "this" : "foreign");

                    m_TeleportThread = ThreadManager.CreateThread(() =>
                    {
                        try
                        {
                            DestinationInfo dInfo;
                            try
                            {
                                if (isSameGrid)
                                {
                                    dInfo = TeleportTo_Step1_ThisGrid(sceneInterface, agent, gatekeeperURI, location, flags);
                                }
                                else
                                {
                                    dInfo = TeleportTo_Step1_ForeignGrid(sceneInterface, agent, gatekeeperURI, location, flags);
                                }
                            }
                            catch (TeleportFailedException e)
                            {
                                m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                                agent.SendAlertMessage(e.Message, sceneInterface.ID);
                                return;
                            }
                            catch (Exception e)
                            {
                                m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                                throw;
                            }
                            try
                            {
                                TeleportTo_Step2(sceneInterface, agent, dInfo, position, lookAt, flags);
                            }
                            catch (Exception e)
                            {
                                m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                                var failedMsg = new TeleportFailed()
                                {
                                    AgentID = agent.ID,
                                    Reason = e.Message
                                };
                                agent.SendMessageIfRootAgent(failedMsg, sceneInterface.ID);
                            }
                        }
                        catch(Exception e)
                        {
                            m_Log.DebugFormat("Exception at teleport handling", e);
                        }
                        finally
                        {
                            agent.RemoveActiveTeleportService(this);
                            m_TeleportThread = null;
                        }
                    });
                    agent.ActiveTeleportService = this;
                    m_TeleportThread.Start();
                    return true;
                }
                else
                {
                    m_Log.DebugFormat("Teleport to {4} grid at {0},{1} requested for {2} not possible: {3}", location.GridX, location.GridY, agent.Owner.FullName, flags.ToString(), isSameGrid ? "this": "foreign");
                }
            }

            return false;
        }

        public override bool TeleportTo(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, UUID regionID, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            bool isSameGrid = gatekeeperURI == sceneInterface.GatekeeperURI;

            lock (m_TeleportThreadLock)
            {
                if (m_TeleportThread == null)
                {
                    m_Log.DebugFormat("Teleport to region {0} at {3} grid requested for {1}: {2}", regionID.ToString(), agent.Owner.FullName, flags.ToString(), isSameGrid ? "this" : "foreign");

                    m_TeleportThread = ThreadManager.CreateThread(() =>
                    {
                        try
                        {
                            DestinationInfo dInfo;
                            try
                            {
                                if (isSameGrid)
                                {
                                    dInfo = TeleportTo_Step1_ThisGrid(sceneInterface, agent, gatekeeperURI, regionID, flags);
                                }
                                else
                                {
                                    dInfo = TeleportTo_Step1_ForeignGrid(sceneInterface, agent, gatekeeperURI, regionID, flags);
                                }
                            }
                            catch (TeleportFailedException e)
                            {
                                m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                                agent.SendAlertMessage(e.Message, sceneInterface.ID);
                                return;
                            }
                            catch (Exception e)
                            {
                                m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                                throw;
                            }
                            try
                            {
                                TeleportTo_Step2(sceneInterface, agent, dInfo, position, lookAt, flags);
                            }
                            catch (Exception e)
                            {
                                m_Log.DebugFormat("Teleport Failed: {0}: {1}\n{2}", e.GetType().FullName, e.Message, e.StackTrace);
                                var failedMsg = new TeleportFailed()
                                {
                                    AgentID = agent.ID,
                                    Reason = e.Message
                                };
                                agent.SendMessageIfRootAgent(failedMsg, sceneInterface.ID);
                            }
                        }
                        finally
                        {
                            m_TeleportThread = null;
                            agent.RemoveActiveTeleportService(this);
                        }
                    });
                    agent.ActiveTeleportService = this;
                    m_TeleportThread.Start();
                    return true;
                }
                else
                {
                    m_Log.DebugFormat("Teleport to region {0} at {3} grid requested for {1} not possible: {2}", regionID.ToString(), agent.Owner.FullName, flags.ToString(), isSameGrid ? "this" : "foreign");
                }
            }

            return false;
        }
        #endregion

        #region Teleport variations setup
        private DestinationInfo TeleportTo_Step1_RegionNameLookup(SceneInterface sceneInterface, IAgent agent, string regionName, TeleportFlags flags)
        {
            DestinationInfo dInfo = null;
            var teleStart = new TeleportStart()
            {
                TeleportFlags = flags
            };
            agent.SendMessageIfRootAgent(teleStart, sceneInterface.ID);

#if DEBUG
            m_Log.DebugFormat("Starting teleport to {0} for {1} ({2})", regionName, agent.Owner.FullName, agent.Owner.ID);
#endif
            var regionAddress = new RegionAddress(regionName);

            if(regionAddress.IsForeignGrid && !regionAddress.TargetsGatekeeperUri(sceneInterface.GatekeeperURI))
            {
                var gatekeeper = new RobustForeignGridConnector(regionAddress.GatekeeperUri);
                RegionInfo rInfo = null;
                string msg;
                if(gatekeeper.TryGetValue(regionAddress.RegionName, out rInfo, out msg))
                {
                    dInfo = new DestinationInfo(rInfo);
                }
                else
                {
                    throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "RegionNotFound", "Region not found."));
                }
            }
            else
            {
                GridServiceInterface gridService = sceneInterface.GridService;
                if (gridService != null)
                {
                    try
                    {
                        RegionInfo rInfo = gridService[sceneInterface.ScopeID, regionName];
                        dInfo = new DestinationInfo(rInfo)
                        {
                            GridURI = sceneInterface.GatekeeperURI,
                            LocalToGrid = true
                        };
                    }
                    catch (KeyNotFoundException)
                    {
                        throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "RegionNotFound", "Region not found."));
                    }
                }
                else
                {
                    throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "RegionNotFound", "Region not found."));
                }
            }

            return dInfo;
        }

        private DestinationInfo TeleportTo_Step1_ThisGrid(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, UUID regionID, TeleportFlags flags)
        {
#if DEBUG
            m_Log.DebugFormat("TeleportTo_Step1_ThisGrid(RegionID): {0} ({1})", agent.Owner.FullName, agent.Owner.ID);
#endif

            var teleStart = new TeleportStart()
            {
                TeleportFlags = flags
            };
            agent.SendMessageIfRootAgent(teleStart, sceneInterface.ID);

            GridServiceInterface gridService = sceneInterface.GridService;

            if (gridService == null)
            {
                throw new TeleportFailedException("No grid service");
            }

            RegionInfo rInfo;
            try
            {
                rInfo = gridService[regionID];
            }
            catch
            {
                throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "RegionNotFound", "Region not found"));
            }

            return new DestinationInfo(rInfo)
            {
                LocalToGrid = true,
                GridURI = gatekeeperURI
            };
        }

        private DestinationInfo TeleportTo_Step1_ForeignGrid(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, UUID regionID, TeleportFlags flags)
        {
#if DEBUG
            m_Log.DebugFormat("TeleportTo_Step1_ForeignGrid(RegionID): {0} ({1})", agent.Owner.FullName, agent.Owner.ID);
#endif

            var teleStart = new TeleportStart
            {
                TeleportFlags = flags
            };
            agent.SendMessageIfRootAgent(teleStart, sceneInterface.ID);

            var gatekeeper = new RobustForeignGridConnector(gatekeeperURI);
            RegionInfo rInfo;
            string msg;
            if (gatekeeper.TryGetRegion(regionID, out rInfo, out msg))
            {
                return new DestinationInfo(rInfo)
                {
                    GridURI = gatekeeperURI,
                    LocalToGrid = false
                };
            }
            else
            {
                throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "RegionNotFound", "Region not found"));
            }
        }

        private DestinationInfo TeleportTo_Step1_ThisGrid(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, GridVector location, TeleportFlags flags)
        {
#if DEBUG
            m_Log.DebugFormat("TeleportTo_Step1_ThisGrid(Location): {0} ({1})", agent.Owner.FullName, agent.Owner.ID);
#endif
            GridServiceInterface gridService = sceneInterface.GridService;
            var teleStart = new TeleportStart()
            {
                TeleportFlags = flags
            };
            agent.SendMessageIfRootAgent(teleStart, sceneInterface.ID);

            if (gridService == null)
            {
                throw new TeleportFailedException("No grid service");
            }

            RegionInfo rInfo;
            try
            {
                rInfo = gridService[sceneInterface.ScopeID, location];
            }
            catch
            {
                throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "RegionNotFound", "Region not found"));
            }

            return new DestinationInfo(rInfo)
            {
                LocalToGrid = true,
                GridURI = gatekeeperURI
            };
        }

        private DestinationInfo TeleportTo_Step1_ForeignGrid(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, GridVector location, TeleportFlags flags)
        {
#if DEBUG
            m_Log.DebugFormat("TeleportTo_Step1_ForeignGrid(Location): {0} ({1})", agent.Owner.FullName, agent.Owner.ID);
#endif
            var teleStart = new TeleportStart
            {
                TeleportFlags = flags
            };
            agent.SendMessageIfRootAgent(teleStart, sceneInterface.ID);

            throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "TeleportNotSupported", "Teleport via location not supported into HG"));
        }
        #endregion

        private void DisconnectAgentFromGrid(IAgent agent, UUID destinationRegionID)
        {
            /* disconnect childs */
            foreach (AgentChildInfo child in agent.ActiveChilds.Values.ToArray())
            {
                try
                {
                    /* do not disconnect destination region */
                    if (child.DestinationInfo.ID != destinationRegionID)
                    {
                        DisableSimulator(child.DestinationInfo.ID, agent, child.DestinationInfo);
                    }
                }
                catch
                {
                    /* ignore fails here */
                }
            }
            Thread.Sleep(2000); /* we have to ensure for a little processing delay due to viewer-sim protocol relationship */
            try
            {
                ReleaseAgent(agent.SceneID);
            }
            catch
            {
                /* ignore fails */
            }
        }

        private void TeleportTo_Step2(SceneInterface scene, IAgent agent, DestinationInfo dInfo, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
#if DEBUG
            m_Log.DebugFormat("TeleportTo_Step2: {0} ({1}) to {2}@{3} ({4},{5})", agent.Owner.FullName, agent.Owner.ID, dInfo.Name, dInfo.GridURI, dInfo.Location.GridX, dInfo.Location.GridY);
#endif
            UUID sceneID = scene.ID;
            uint actualCircuitCode;
            var vagent = (ViewerAgent)agent;
            AgentCircuit circ = vagent.Circuits[sceneID];
            actualCircuitCode = circ.CircuitCode;

            SendTeleportProgress(agent, sceneID, this.GetLanguageString(agent.CurrentCulture, "ConnectDestinationSimulator", "Connecting to destination simulator"), flags);

            if (scene.GatekeeperURI == dInfo.GridURI)
            {
                if (dInfo.ServerURI == scene.ServerURI)
                {
                    SceneInterface targetScene;
                    /* it is us, so we can go for a simplified local protocol */
                    if (m_Scenes.TryGetValue(dInfo.ID, out targetScene))
                    {
                        RwLockedDictionary<UUID, AgentChildInfo> neighbors = agent.ActiveChilds;
                        AgentChildInfo childInfo;
                        AgentCircuit targetCircuit;
                        if (!neighbors.TryGetValue(dInfo.ID, out childInfo) &&
                            !vagent.Circuits.TryGetValue(dInfo.ID, out targetCircuit))
                        {
                            UUID seedId = UUID.Random;
                            string seedUri = NewCapsURL(dInfo.ServerURI, seedId);
                            var ep = new IPEndPoint(((IPEndPoint)circ.RemoteEndPoint).Address, 0);
                            targetCircuit = new AgentCircuit(
                                Commands,
                                vagent,
                                (UDPCircuitsManager)targetScene.UDPServer,
                                actualCircuitCode,
                                m_CapsRedirector,
                                seedId,
                                vagent.ServiceURLs,
                                dInfo.GridURI,
                                m_PacketHandlerPlugins,
                                ep)
                            {
                                Agent = vagent,
                                AgentID = vagent.ID,
                                SessionID = vagent.Session.SessionID,
                                LastTeleportFlags = flags
                            };
                            vagent.Circuits.Add(targetCircuit.Scene.ID, targetCircuit);

                            try
                            {
                                scene.Add(agent);
                                try
                                {
                                    ((UDPCircuitsManager)targetScene.UDPServer).AddCircuit(targetCircuit);
                                }
                                catch(Exception e)
                                {
                                    m_Log.Debug("Failed agent add", e);
                                    scene.Remove(agent);
                                    throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "AddAgentFailed", "Adding agent failed"));
                                }
                            }
                            catch (Exception e)
                            {
                                m_Log.Debug("Failed agent add", e);
                                vagent.Circuits.Remove(targetScene.ID);
                                throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "AddAgentFailed", "Adding agent failed"));
                            }

                            SendTeleportProgress(agent, sceneID, this.GetLanguageString(agent.CurrentCulture, "TransferingToDestination", "Transfering to destination"), flags);

                            /* the moment we send this, there is no way to get the viewer back if something fails and the viewer connected successfully on other side */
                            var teleFinish = new TeleportFinish
                            {
                                AgentID = agent.ID,
                                LocationID = 4,
                                SimIP = ((IPEndPoint)dInfo.SimIP).Address,
                                SimPort = (ushort)dInfo.ServerPort,
                                GridPosition = dInfo.Location,
                                SeedCapability = seedUri,
                                SimAccess = dInfo.Access,
                                TeleportFlags = flags,
                                RegionSize = dInfo.Size
                            };
                            agent.SendMessageIfRootAgent(teleFinish, scene.ID);

                            targetCircuit.LogIncomingAgent(m_Log, false);
                        }
                        else if (!vagent.Circuits.TryGetValue(dInfo.ID, out targetCircuit))
                        {
                            throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "LocalTeleportDestinationNotAvailable", "Local teleport destination not available"));
                        }
                        else
                        {
                            targetCircuit.LastTeleportFlags = flags;

                            SendTeleportProgress(agent, sceneID, this.GetLanguageString(agent.CurrentCulture, "TransferingToDestination", "Transfering to destination"), flags);

                            /* the moment we send this, there is no way to get the viewer back if something fails and the viewer connected successfully on other side */
                            var teleFinish = new TeleportFinish
                            {
                                AgentID = agent.ID,
                                LocationID = 4,
                                SimIP = ((IPEndPoint)dInfo.SimIP).Address,
                                SimPort = (ushort)dInfo.ServerPort,
                                GridPosition = dInfo.Location,
                                SeedCapability = childInfo.SeedCapability,
                                SimAccess = dInfo.Access,
                                TeleportFlags = flags,
                                RegionSize = dInfo.Size
                            };
                            agent.SendMessageIfRootAgent(teleFinish, scene.ID);

                            targetCircuit.LogIncomingAgent(m_Log, false);
                        }
                        /* we just finished the teleport code. Most stuff handles straight through UDP handling as we share the agent instance through all scenes */
                    }
                    else
                    {
                        throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "RegionNotFound", "Region not found"));
                    }
                }
                else
                {
                    /* we have to check for active neighbor */
                    RwLockedDictionary<UUID, AgentChildInfo> neighbors = agent.ActiveChilds;
                    AgentChildInfo childInfo;
                    uint circuitCode;
                    string capsPath;

                    double protoVersion = QueryAccess(dInfo, agent, position);
                    if (protoVersion < 0.2)
                    {
                        throw new TeleportFailedException("Older teleport variant not yet implemented");
                    }
                    var maxWearables = (int)WearableType.NumWearables;
                    if(protoVersion < 0.4)
                    {
                        maxWearables = 15;
                    }

                    if (neighbors.TryGetValue(dInfo.ID, out childInfo))
                    {
                        /* we have to use PostAgent for TeleportFlags essentially */
                        try
                        {
                            PostAgent(scene.ID, agent, dInfo, childInfo.SeedCapsID, maxWearables, out circuitCode, out capsPath);
                        }
                        catch (Exception e)
                        {
                            string msg = e.Message;
                            if (string.IsNullOrEmpty(msg))
                            {
                                msg = "Teleport failed due to communication failure.";
                            }
                            throw new TeleportFailedException(msg);
                        }
                    }
                    else
                    {
                        /* no child connection, so we need a new one */
                        try
                        {
                            PostAgent(scene.ID, agent, dInfo, maxWearables, out circuitCode, out capsPath);
                        }
                        catch (Exception e)
                        {
                            string msg = e.Message;
                            if (string.IsNullOrEmpty(msg))
                            {
                                msg = "Teleport failed due to communication failure.";
                            }
                            throw new TeleportFailedException(msg);
                        }
                    }

                    SendTeleportProgress(agent, sceneID, this.GetLanguageString(agent.CurrentCulture, "TransferingToDestination", "Transfering to destination"), flags);

                    /* the moment we send this, there is no way to get the viewer back if something fails and the viewer connected successfully on other side */
                    var teleFinish = new TeleportFinish
                    {
                        AgentID = agent.ID,
                        LocationID = 0,
                        SimIP = ((IPEndPoint)dInfo.SimIP).Address,
                        SimPort = (ushort)dInfo.ServerPort,
                        GridPosition = dInfo.Location,
                        SeedCapability = capsPath,
                        SimAccess = dInfo.Access,
                        TeleportFlags = flags,
                        RegionSize = dInfo.Size
                    };
                    agent.SendMessageIfRootAgent(teleFinish, scene.ID);

                    if (protoVersion < 0.2)
                    {
                        PutAgent(sceneID, dInfo, agent, circuitCode, maxWearables, false, BuildAgentUri(scene.GetRegionInfo(), agent, "/release"));
                    }
                    else
                    {
                        if (!PutAgent(sceneID, dInfo, agent, circuitCode, maxWearables, true))
                        {
                            /* TODO: check if there is any possibility to know whether viewer is still with us */
                            throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "FailedToEstablishViewerConnectionOnRemote", "Failed to establish viewer connection on remote simulator"));
                        }

                        /* agent is over there */

                        /* TODO: handle disconnect of child agents */
                        /* remotes are disconnecting too so we simply leave it to them */
                        agent.SceneID = UUID.Zero;
                    }
                }
            }
            else
            {
                double protoVersion = QueryAccess(dInfo, agent, position);
                uint circuitCode;
                string capsPath;

                string homeAgentUri = agent.Owner.HomeURI.ToString();
                if (!homeAgentUri.EndsWith("/"))
                {
                    homeAgentUri += "/";
                }
                homeAgentUri += "homeagent/" + agent.ID;

#if DEBUG
                m_Log.DebugFormat("Sending agent POST to {0} for {1} ({2})", homeAgentUri, agent.Owner.FullName, agent.Owner.ID);
#endif

                if (protoVersion < 0.2)
                {
                    throw new TeleportFailedException("Older teleport variant not yet implemented");
                }
                var maxWearables = (int)WearableType.NumWearables;
                if (protoVersion < 0.4)
                {
                    maxWearables = 15;
                }

                try
                {
                    PostAgent(scene.ID, agent, dInfo, UUID.Random, maxWearables, homeAgentUri, out circuitCode, out capsPath);
                }
                catch (Exception e)
                {
                    string msg = e.Message;
                    if (string.IsNullOrEmpty(msg))
                    {
                        msg = "Teleport failed due to communication failure.";
                    }
                    throw new TeleportFailedException(msg);
                }

                SendTeleportProgress(agent, sceneID, this.GetLanguageString(agent.CurrentCulture, "TransferingToDestination", "Transfering to destination"), flags);

                /* the moment we send this, there is no way to get the viewer back if something fails and the viewer connected successfully on other side */
                var teleFinish = new TeleportFinish
                {
                    AgentID = agent.ID,
                    LocationID = 0,
                    SimIP = ((IPEndPoint)dInfo.SimIP).Address,
                    SimPort = (ushort)dInfo.ServerPort,
                    GridPosition = dInfo.Location,
                    SeedCapability = capsPath,
                    SimAccess = dInfo.Access,
                    TeleportFlags = flags,
                    RegionSize = dInfo.Size
                };
                agent.SendMessageIfRootAgent(teleFinish, scene.ID);

                if (protoVersion < 0.2)
                {
                    PutAgent(sceneID, dInfo, agent, circuitCode, maxWearables, false, BuildAgentUri(scene.GetRegionInfo(), agent, "/release"));
                }
                else
                {
                    if (!PutAgent(sceneID, dInfo, agent, circuitCode, maxWearables, true))
                    {
                        /* TODO: check if there is any possibility to know whether viewer is still with us */
                        throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "FailedToEstablishViewerConnectionOnRemote", "Failed to establish viewer connection on remote simulator"));
                    }

                    /* agent is over there */

                    /* disconnect of child agents */
                    DisconnectAgentFromGrid(agent, dInfo.ID);

                    /* remotes are disconnecting too so we simply leave it to them */
                    agent.SceneID = UUID.Zero;
                }
            }
        }

        private static void SendTeleportProgress(IAgent agent, UUID sceneID, string message, TeleportFlags flags)
        {
            var progressMsg = new TeleportProgress()
            {
                AgentID = agent.ID,
                TeleportFlags = flags,
                Message = message
            };
            agent.SendMessageIfRootAgent(progressMsg, sceneID);
        }

        #region Query Access
        internal struct ProtocolVersion
        {
            public uint Major;
            public uint Minor;

            public ProtocolVersion(string version, int startOfNumbers = 11)
            {
                string[] verparts = version.Substring(startOfNumbers).Split('.');
                Major = uint.Parse(verparts[0]);
                Minor = verparts.Length > 1 ? uint.Parse(verparts[1]) : 0;
            }
        }

        private double QueryAccess(DestinationInfo dInfo, IAgent agent, Vector3 position)
        {
            string uri = BuildAgentUri(dInfo, agent);
#if DEBUG
            m_Log.DebugFormat("QueryAccess: {0} ({1}) at {2}", agent.Owner.FullName, agent.Owner.ID, uri);
#endif

            string versionStr = string.Format("{0}.{1}", PROTOCOL_VERSION_MAJOR, PROTOCOL_VERSION_MINOR);
            double maxVersion = double.Parse(versionStr, System.Globalization.CultureInfo.InvariantCulture);
            var req = new Map
            {
                { "position", position.ToString() },
                { "my_version", "SIMULATION/" + versionStr },
                { "simulation_service_supported_min", 0.3 },
                { "simulation_service_supported_max", maxVersion },
                { "simulation_service_accepted_min", 0.3 },
                { "simulation_service_accepted_max", maxVersion }
            };
            var entityctx = new Map
            {
                { "InboundVersion", maxVersion },
                { "OutboundVersion", maxVersion },
                { "WearablesCount", (int)WearableType.NumWearables }
            };
            req.Add("context", entityctx);
            var features = new AnArray();
            req.Add("features", features);
            if (agent.Owner.HomeURI != null)
            {
                req.Add("agent_home_uri", agent.Owner.HomeURI);
            }

            Map jsonres;
            using (Stream s = new HttpClient.Request(uri)
            {
                Method = "QUERYACCESS",
                RequestContentType = "application/json",
                RequestBody = Json.Serialize(req),
                TimeoutMs = TimeoutMs
            }.ExecuteStreamRequest())
            {
                jsonres = Json.Deserialize(s) as Map;
            }

            if (jsonres == null)
            {
                throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "TeleportProtocolError", "Teleport Protocol Error"));
            }
            if (!jsonres["success"].AsBoolean)
            {
                throw new TeleportFailedException(jsonres["reason"].ToString());
            }

            double protoVersion;
            IValue versionAsDouble;
            if (jsonres.TryGetValue("negotiated_outbound_version", out versionAsDouble))
            {
                protoVersion = double.Parse(versionAsDouble.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                string version = jsonres["version"].ToString();
                if (!version.StartsWith("SIMULATION/"))
                {
                    throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "TeleportProtocolError", "Teleport Protocol Error"));
                }
                protoVersion = double.Parse(version.Substring(11));
            }

            if (protoVersion > PROTOCOL_VERSION_MAJOR + PROTOCOL_VERSION_MINOR / 10.0)
            {
                protoVersion = PROTOCOL_VERSION_MAJOR + PROTOCOL_VERSION_MINOR / 10.0;
            }
            else if (protoVersion > PROTOCOL_VERSION_MAJOR + PROTOCOL_VERSION_MINOR / 10.0)
            {
                protoVersion = PROTOCOL_VERSION_MAJOR + PROTOCOL_VERSION_MINOR / 10.0;
            }
            return protoVersion;
        }
        #endregion

        #region PutAgent Handler
        private bool PutAgent(UUID fromSceneID, RegionInfo dInfo, IAgent agent, uint circuitcode, int maxAllowedWearables, bool waitForRoot = false, string callbackUri = "")
        {
            string uri = BuildAgentUri(dInfo, agent);

            var req = new Map
            {
                { "destination_x", dInfo.Location.X },
                { "destination_y", dInfo.Location.Y },
                { "destination_name", dInfo.Name },
                { "destination_uuid", dInfo.ID },
                { "message_type", "AgentData" },
                { "region_id", fromSceneID },

                { "circuit_code", circuitcode.ToString() },
                { "agent_uuid", agent.ID },
                { "session_uuid", agent.Session.SessionID },
                { "position", agent.GlobalPosition.ToString() },
                { "velocity", agent.Velocity.ToString() },
                //req.Add("center", )
                { "size", agent.Size.ToString() },
                { "at_axis", agent.CameraAtAxis.ToString() },
                { "left_axis", agent.CameraLeftAxis.ToString() },
                { "up_axis", agent.CameraUpAxis.ToString() },
                { "changed_grid", waitForRoot },
                { "wait_for_root", waitForRoot },
                //req.Add("far", agent.);
                //req.Add("aspect", agent.);
                //req.Add("locomotion_state", agent.);
                //req.Add("head_rotation", agent.)
                { "body_rotation", agent.BodyRotation.ToString() },
                //req.Add("control_flags", agent.);
                { "energy_level", agent.Health }
            };
            //req.Add("god_level", agent.)
            //req.Add("always_run", )
            //req.Add("prey_agent", );
            //req.Add("agent_access", );
            if (agent.Group != null)
            {
                req.Add("active_group_id", agent.Group.ID);
            }
            //req.Add("groups", new AnArray());
            /*
            if ((Anims != null) && (Anims.Length > 0))
            {
                OSDArray anims = new OSDArray(Anims.Length);
                foreach (Animation aanim in Anims)
                    anims.Add(aanim.PackUpdateMessage());
                args["animations"] = anims;
            }
            if (DefaultAnim != null)
            {
                args["default_animation"] = DefaultAnim.PackUpdateMessage();
            }
            if (AnimState != null)
            {
                args["animation_state"] = AnimState.PackUpdateMessage();
            }
            */

            /*-----------------------------------------------------------------*/
            /* Appearance */
            var appearancePack = new Map();
            AppearanceInfo appearance = agent.Appearance;

            appearancePack.Add("height", appearance.AvatarHeight);

            {
                var vParams = new AnArray();
                foreach(byte vp in appearance.VisualParams)
                {
                    vParams.Add(vp);
                }
                appearancePack.Add("visualparams", vParams);
            }

            {
                var texArray = new AnArray();
                int i;
                for (i = 0; i < AppearanceInfo.AvatarTextureData.TextureCount; ++i)
                {
                    texArray.Add(appearance.AvatarTextures[i]);
                }
                appearancePack.Add("textures", texArray);
            }

            {
                int i;
                var wearables = new AnArray();
                for (i = 0; i < (int)WearableType.NumWearables && i < maxAllowedWearables; ++i)
                {
                    AnArray ar;
                    try
                    {
                        ar = (AnArray)wearables[i];
                    }
                    catch
                    {
                        wearables.Add(new AnArray());
                        continue;
                    }
                    List<AgentWearables.WearableInfo> wearablesList = appearance.Wearables[(WearableType)i];
                    var wearablesBlock = new AnArray();
                    foreach(AgentWearables.WearableInfo wearable in wearablesList)
                    {
                        var wp = new Map
                        {
                            ["item"] = wearable.ItemID
                        };
                        if (wearable.AssetID != UUID.Zero)
                        {
                            wp.Add("asset", wearable.AssetID);
                        }
                        wearablesBlock.Add(wp);
                    }
                    wearables.Add(wearablesBlock);
                }
                appearancePack.Add("wearables", wearables);
            }

            {
                var attachments = new AnArray();
                foreach(KeyValuePair<AttachmentPoint, RwLockedDictionary<UUID, UUID>> kvpOuter in appearance.Attachments)
                {
                    foreach(KeyValuePair<UUID, UUID> kvp in kvpOuter.Value)
                    {
                        var ap = new Map
                        {
                            { "point", (int)kvpOuter.Key },
                            { "item", kvp.Key }
                        };
                        attachments.Add(ap);
                    }
                }
                appearancePack["attachments"] = attachments;
            }

            appearancePack.Add("serial", appearance.Serial);
            req.Add("packed_appearance", appearancePack);

            /*
            if ((args["controllers"] != null) && (args["controllers"]).Type == OSDType.Array)
            {
                OSDArray controls = (OSDArray)(args["controllers"]);
                Controllers = new ControllerData[controls.Count];
                int i = 0;
                foreach (OSD o in controls)
                {
                    if (o.Type == OSDType.Map)
                    {
                        Controllers[i++] = new ControllerData((OSDMap)o);
                     * 
                        public void UnpackUpdateMessage(OSDMap args)
                        {
                            if (args["object"] != null)
                                ObjectID = args["object"].AsUUID();
                            if (args["item"] != null)
                                ItemID = args["item"].AsUUID();
                            if (args["ignore"] != null)
                                IgnoreControls = (uint)args["ignore"].AsInteger();
                            if (args["event"] != null)
                                EventControls = (uint)args["event"].AsInteger();
                        }
                                     * 
                    }
                }
            }
            */
            if (!string.IsNullOrEmpty(callbackUri))
            {
                req.Add("callback_uri", callbackUri);
            }

            /*
            // Attachment objects
            if (args["attach_objects"] != null && args["attach_objects"].Type == OSDType.Array)
            {
            OSDArray attObjs = (OSDArray)(args["attach_objects"]);
            AttachmentObjects = new List<ISceneObject>();
            AttachmentObjectStates = new List<string>();
            foreach (OSD o in attObjs)
            {
            if (o.Type == OSDType.Map)
            {
                OSDMap info = (OSDMap)o;
                ISceneObject so = scene.DeserializeObject(info["sog"].AsString());
                so.ExtraFromXmlString(info["extra"].AsString());
                so.HasGroupChanged = info["modified"].AsBoolean();
                AttachmentObjects.Add(so);
                AttachmentObjectStates.Add(info["state"].AsString());
            }
            }
            }

            if (args["parent_part"] != null)
            ParentPart = args["parent_part"].AsUUID();
            if (args["sit_offset"] != null)
            Vector3.TryParse(args["sit_offset"].AsString(), out SitOffset);
            */
            IObject obj = agent.SittingOnObject;
            if (obj != null)
            {
                req.Add("parent_part", obj.LocalID);
                req.Add("sit_offset", agent.LocalPosition);
            }

            byte[] uncompressed_postdata;
            using (var ms = new MemoryStream())
            {
                Json.Serialize(req, ms);
                uncompressed_postdata = ms.ToArray();
            }

            string resultStr;

            byte[] compressed_postdata;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionMode.Compress))
                {
                    gz.Write(uncompressed_postdata, 0, uncompressed_postdata.Length);
                }
                compressed_postdata = ms.ToArray();
            }
            try
            {
                using (Stream o = new HttpClient.Put(uri, "application/json", compressed_postdata.Length, (Stream ws) =>
                    ws.Write(compressed_postdata, 0, compressed_postdata.Length))
                {
                    IsCompressed = true,
                    TimeoutMs = TimeoutMs
                }.ExecuteStreamRequest())
                using (StreamReader reader = o.UTF8StreamReader())
                {
                    resultStr = reader.ReadToEnd();
                }
            }
            catch
            {
                try
                {
                    using (Stream o = new HttpClient.Put(uri, "application/x-gzip", compressed_postdata.Length, (Stream ws) =>
                        ws.Write(compressed_postdata, 0, compressed_postdata.Length))
                    {
                        TimeoutMs = TimeoutMs
                    }.ExecuteStreamRequest())
                    using (StreamReader reader = o.UTF8StreamReader())
                    {
                        resultStr = reader.ReadToEnd();
                    }
                }
                catch
                {
                    try
                    {
                        using (Stream o = new HttpClient.Put(uri, "application/json", uncompressed_postdata.Length, (Stream ws) =>
                            ws.Write(uncompressed_postdata, 0, uncompressed_postdata.Length))
                        {
                            TimeoutMs = TimeoutMs
                        }.ExecuteStreamRequest())
                        using (StreamReader reader = o.UTF8StreamReader())
                        {
                            resultStr = reader.ReadToEnd();
                        }
                    }
                    catch (Exception e)
                    {
                        m_Log.Error("Connect failed", e);
                        /* connect failed */
                        return false;
                    }
                }
            }

            if(resultStr.ToLower() == "true")
            {
                return true;
            }
            else if(resultStr.ToLower() == "false")
            {
                return false;
            }
            else
            {
                throw new TeleportFailedException("Protocol Error");
            }
        }
        #endregion
    }
}
