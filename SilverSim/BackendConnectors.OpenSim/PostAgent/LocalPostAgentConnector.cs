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
using SilverSim.BackendConnectors.OpenSim.Teleport;
using SilverSim.BackendConnectors.Robust.Asset;
using SilverSim.BackendConnectors.Robust.Friends;
using SilverSim.BackendConnectors.Robust.GridUser;
using SilverSim.BackendConnectors.Robust.IM;
using SilverSim.BackendConnectors.Robust.Inventory;
using SilverSim.BackendConnectors.Robust.Presence;
using SilverSim.BackendConnectors.Robust.UserAgent;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.CmdIO;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Agent;
using SilverSim.Scene.Types.Scene;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.Authorization;
using SilverSim.ServiceInterfaces.Economy;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.GridUser;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.ServiceInterfaces.Profile;
using SilverSim.ServiceInterfaces.UserAgents;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Agent;
using SilverSim.Types.Presence;
using SilverSim.Viewer.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;

namespace SilverSim.BackendConnectors.OpenSim.PostAgent
{
    [PluginName("LocalPostAgentConnector")]
    [Description("Local Post Agent Connector")]
    public class LocalPostAgentConnector : PostAgentConnector
    {
        private SceneList Scenes;
        private readonly static ILog m_Log = LogManager.GetLogger("LOCAL POST AGENT");

        private BaseHttpServer m_HttpServer;
        private Main.Common.Caps.CapsHttpRedirector m_CapsRedirector;
        private readonly string m_DefaultGridUserServerURI = string.Empty;
        private readonly string m_DefaultPresenceServerURI = string.Empty;
        private readonly Dictionary<string, IAssetServicePlugin> m_AssetServicePlugins = new Dictionary<string, IAssetServicePlugin>();
        private readonly Dictionary<string, IInventoryServicePlugin> m_InventoryServicePlugins = new Dictionary<string, IInventoryServicePlugin>();
        private readonly Dictionary<string, IProfileServicePlugin> m_ProfileServicePlugins = new Dictionary<string, IProfileServicePlugin>();
        private List<IProtocolExtender> m_PacketHandlerPlugins = new List<IProtocolExtender>();
        private List<AuthorizationServiceInterface> m_AuthorizationServices;
        private CommandRegistry Commands;
        private string m_HomeURI;
        public GridUserServiceInterface m_GridUserService;
        public string m_GridUserServiceName = string.Empty;

        private class StandaloneServicesContainer
        {
            public ProfileServiceInterface ProfileService;
            public string ProfileServiceName = string.Empty;
            public PresenceServiceInterface PresenceService;
            public string PresenceServiceName = string.Empty;
            public FriendsServiceInterface FriendsService;
            public string FriendsServiceName = string.Empty;
            public OfflineIMServiceInterface OfflineIMService;
            public string OfflineIMServiceName = string.Empty;
            public AssetServiceInterface AssetService;
            public string AssetServiceName = string.Empty;
            public InventoryServiceInterface InventoryService;
            public string InventoryServiceName = string.Empty;
        }

        private StandaloneServicesContainer StandaloneServices;

        private Dictionary<string, EconomyServiceInterface> EconomyServices;

        private sealed class GridParameterMap : ICloneable
        {
            public string HomeURI;
            public string AssetServerURI;
            public string InventoryServerURI;
            public string GridUserServerURI = string.Empty;
            public string PresenceServerURI = string.Empty;
            public string AvatarServerURI = string.Empty;
            public string FriendsServerURI = string.Empty;
            public string GatekeeperURI = string.Empty;
            public string ProfileServerURI = string.Empty;
            public string OfflineIMServerURI = string.Empty;
            public readonly List<UUID> ValidForSims = new List<UUID>(); /* if empty, all sims are valid */

            public object Clone()
            {
                var m = new GridParameterMap
                {
                    HomeURI = HomeURI,
                    GatekeeperURI = GatekeeperURI,
                    AssetServerURI = AssetServerURI,
                    InventoryServerURI = InventoryServerURI,
                    GridUserServerURI = GridUserServerURI,
                    PresenceServerURI = PresenceServerURI,
                    AvatarServerURI = AvatarServerURI,
                    FriendsServerURI = FriendsServerURI,
                    ProfileServerURI = ProfileServerURI,
                    OfflineIMServerURI = OfflineIMServerURI
                };
                m.ValidForSims.AddRange(ValidForSims);
                return m;
            }
        }

        private readonly RwLockedList<GridParameterMap> m_GridParameterMap = new RwLockedList<GridParameterMap>();

        public LocalPostAgentConnector(IConfig ownSection)
        {
            m_DefaultGridUserServerURI = ownSection.GetString("DefaultGridUserServerURI", string.Empty);
            m_DefaultPresenceServerURI = ownSection.GetString("DefaultPresenceServerURI", string.Empty);
            m_GridUserServiceName = ownSection.GetString("GridUserService", "GridUserService");
            if (ownSection.GetBoolean("IsStandalone", false))
            {
                StandaloneServices = new StandaloneServicesContainer()
                {
                    FriendsServiceName = ownSection.GetString("FriendsService", "FriendsService"),
                    OfflineIMServiceName = ownSection.GetString("OfflineIMService", "OfflineIMService"),
                    PresenceServiceName = ownSection.GetString("PresenceService", "PresenceService"),
                    ProfileServiceName = ownSection.GetString("ProfileService", "ProfileService"),
                    AssetServiceName = ownSection.GetString("AssetService", "AssetService"),
                    InventoryServiceName = ownSection.GetString("InventoryService", "InventoryService")
                };
            }
        }

        protected string HeloRequester(string uri)
        {
            if (!uri.EndsWith("="))
            {
                uri = uri.TrimEnd('/') + "/helo/";
            }
            else
            {
                /* simian special */
                if (uri.Contains("?"))
                {
                    uri = uri.Substring(0, uri.IndexOf('?'));
                }
                uri = uri.TrimEnd('/') + "/helo/";
            }

            var headers = new Dictionary<string, string>();
            try
            {
                new HttpClient.Head(uri)
                {
                    Headers = headers
                }.ExecuteRequest();

                if (!headers.ContainsKey("x-handlers-provided"))
                {
                    return "opensim-robust"; /* let us assume Robust API */
                }
                return headers["x-handlers-provided"];
            }
            catch
            {
                return "opensim-robust"; /* let us assume Robust API */
            }
        }

        public override void Startup(ConfigurationLoader loader)
        {
            base.Startup(loader);
            EconomyServices = loader.GetServicesByKeyValue<EconomyServiceInterface>();
            m_HttpServer = loader.HttpServer;
            m_CapsRedirector = loader.CapsRedirector;
            Scenes = loader.Scenes;
            Commands = loader.CommandRegistry;
            m_HomeURI = loader.HomeURI;
            m_GridUserService = loader.GetService<GridUserServiceInterface>(m_GridUserServiceName);

            if (StandaloneServices != null)
            {
                StandaloneServices.FriendsService = loader.GetService<FriendsServiceInterface>(StandaloneServices.FriendsServiceName);
                StandaloneServices.OfflineIMService = loader.GetService<OfflineIMServiceInterface>(StandaloneServices.OfflineIMServiceName);
                StandaloneServices.PresenceService = loader.GetService<PresenceServiceInterface>(StandaloneServices.PresenceServiceName);
                StandaloneServices.ProfileService = loader.GetService<ProfileServiceInterface>(StandaloneServices.ProfileServiceName);
                StandaloneServices.AssetService = loader.GetService<AssetServiceInterface>(StandaloneServices.AssetServiceName);
                StandaloneServices.InventoryService = loader.GetService<InventoryServiceInterface>(StandaloneServices.InventoryServiceName);
            }

            m_AuthorizationServices = loader.GetServicesByValue<AuthorizationServiceInterface>();

            foreach (IAssetServicePlugin plugin in loader.GetServicesByValue<IAssetServicePlugin>())
            {
                m_AssetServicePlugins.Add(plugin.Name, plugin);
            }
            foreach (IInventoryServicePlugin plugin in loader.GetServicesByValue<IInventoryServicePlugin>())
            {
                m_InventoryServicePlugins.Add(plugin.Name, plugin);
            }
            foreach (IProfileServicePlugin plugin in loader.GetServicesByValue<IProfileServicePlugin>())
            {
                m_ProfileServicePlugins.Add(plugin.Name, plugin);
            }

            m_PacketHandlerPlugins = loader.GetServicesByValue<IProtocolExtender>();

            foreach (IConfig section in loader.Config.Configs)
            {
                if (section.Name.StartsWith("RobustGrid-"))
                {
                    if (!section.Contains("HomeURI") || !section.Contains("AssetServerURI") || !section.Contains("InventoryServerURI"))
                    {
                        m_Log.WarnFormat("Skipping section {0} for missing entries (HomeURI, AssetServerURI and InventoryServerURI are required)", section.Name);
                        continue;
                    }
                    var map = new GridParameterMap();
                    map.HomeURI = section.GetString("HomeURI");
                    if (string.IsNullOrEmpty(map.HomeURI))
                    {
                        map.HomeURI = m_HttpServer.ServerURI;
                        if (!map.HomeURI.EndsWith("/"))
                        {
                            map.HomeURI += "/";
                        }
                    }

                    map.AssetServerURI = section.GetString("AssetServerURI", map.HomeURI);
                    map.GridUserServerURI = section.GetString("GridUserServerURI", m_DefaultGridUserServerURI);
                    map.PresenceServerURI = section.GetString("PresenceServerURI", string.Empty);
                    map.AvatarServerURI = section.GetString("AvatarServerURI", string.Empty);
                    map.InventoryServerURI = section.GetString("InventoryServerURI", map.HomeURI);
                    map.OfflineIMServerURI = section.GetString("OfflineIMServerURI", string.Empty);
                    map.FriendsServerURI = section.GetString("FriendsServerURI", string.Empty);

                    if (!Uri.IsWellFormedUriString(map.HomeURI, UriKind.Absolute))
                    {
                        m_Log.WarnFormat("Skipping section {0} for invalid URI in HomeURI {1}", section.Name, map.HomeURI);
                        continue;
                    }
                    else if (map.GridUserServerURI.Length != 0 && !Uri.IsWellFormedUriString(map.GridUserServerURI, UriKind.Absolute))
                    {
                        m_Log.WarnFormat("Skipping section {0} for invalid URI in GridUserServerURI {1}", section.Name, map.GridUserServerURI);
                        continue;
                    }
                    else if (map.GatekeeperURI.Length != 0 && !Uri.IsWellFormedUriString(map.GatekeeperURI, UriKind.Absolute))
                    {
                        m_Log.WarnFormat("Skipping section {0} for invalid URI in GatekeeperURI {1}", section.Name, map.GatekeeperURI);
                        continue;
                    }
                    else if (map.FriendsServerURI.Length != 0 && !Uri.IsWellFormedUriString(map.FriendsServerURI, UriKind.Absolute))
                    {
                        m_Log.WarnFormat("Skipping section {0} for invalid URI in FriendsServerURI {1}", section.Name, map.FriendsServerURI);
                        continue;
                    }
                    else if (map.PresenceServerURI.Length != 0 && !Uri.IsWellFormedUriString(map.PresenceServerURI, UriKind.Absolute))
                    {
                        m_Log.WarnFormat("Skipping section {0} for invalid URI in PresenceServerURI {1}", section.Name, map.PresenceServerURI);
                        continue;
                    }
                    else if (map.AvatarServerURI.Length != 0 && !Uri.IsWellFormedUriString(map.AvatarServerURI, UriKind.Absolute))
                    {
                        m_Log.WarnFormat("Skipping section {0} for invalid URI in AvatarServerURI {1}", section.Name, map.AvatarServerURI);
                        continue;
                    }
                    else if (map.OfflineIMServerURI.Length != 0 && !Uri.IsWellFormedUriString(map.OfflineIMServerURI, UriKind.Absolute))
                    {
                        m_Log.WarnFormat("Skipping section {0} for invalid URI in OfflineIMServerURI {1}", section.Name, map.OfflineIMServerURI);
                        continue;
                    }
                    else if (!Uri.IsWellFormedUriString(map.AssetServerURI, UriKind.Absolute))
                    {
                        m_Log.WarnFormat("Skipping section {0} for invalid URI in AssetServerURI {1}", section.Name, map.AssetServerURI);
                        continue;
                    }
                    else if (!Uri.IsWellFormedUriString(map.InventoryServerURI, UriKind.Absolute))
                    {
                        m_Log.WarnFormat("Skipping section {0} for invalid URI in InventoryServerURI {1}", section.Name, map.InventoryServerURI);
                        continue;
                    }

                    if (section.Contains("ValidFor"))
                    {
                        string[] sims = section.GetString("ValidFor", string.Empty).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (sims.Length != 0)
                        {
                            foreach (string sim in sims)
                            {
                                UUID id;
                                if (!UUID.TryParse(sim.Trim(), out id))
                                {
                                    m_Log.ErrorFormat("Invalid UUID {0} encountered within ValidFor in section {1}", sim, section.Name);
                                    continue;
                                }
                                map.ValidForSims.Add(id);
                            }
                            if (map.ValidForSims.Count == 0)
                            {
                                m_Log.WarnFormat("Grid Parameter Map section {0} will be valid for all sims", section.Name);
                            }
                        }
                    }
                    m_GridParameterMap.Add(map);
                    foreach (string key in section.GetKeys())
                    {
                        if (key.StartsWith("Alias-"))
                        {
                            var map2 = (GridParameterMap)map.Clone();
                            map2.HomeURI = section.GetString(key);
                            m_GridParameterMap.Add(map2);
                        }
                    }
                }
            }

            m_CapsRedirector = loader.CapsRedirector;
        }

        private GridParameterMap FindGridParameterMap(string homeURI, SceneInterface scene)
        {
            foreach (GridParameterMap map in m_GridParameterMap)
            {
                /* some grids have weird ideas about how to map their URIs, so we need this checking for whether the grid name starts with it */
                if (map.HomeURI.StartsWith(homeURI))
                {
                    if (map.ValidForSims.Count != 0 &&
                        !map.ValidForSims.Contains(scene.ID))
                    {
                        continue;
                    }
                    return map;
                }
            }
            return null;
        }

        public override void PostAgent(CircuitInfo circuitInfo, AuthorizationServiceInterface.AuthorizationData authData)
        {
            SceneInterface scene;
            if (!Scenes.TryGetValue(authData.DestinationInfo.ID, out scene))
            {
                m_Log.InfoFormat("No destination for agent {0}", authData.AccountInfo.Principal.FullName);
                throw new OpenSimTeleportProtocol.TeleportFailedException("No destination for agent " + authData.AccountInfo.Principal.FullName);
            }

            string assetServerURI = authData.AccountInfo.ServiceURLs["AssetServerURI"];
            string inventoryServerURI = authData.AccountInfo.ServiceURLs["InventoryServerURI"];
            string gatekeeperURI = scene.GatekeeperURI;

            AssetServiceInterface assetService = null;
            InventoryServiceInterface inventoryService = null;
            ProfileServiceInterface profileService = null;
            UserAgentServiceInterface userAgentService;
            PresenceServiceInterface presenceService = null;
            GridUserServiceInterface gridUserService = null;
            FriendsServiceInterface friendsService = null;
            OfflineIMServiceInterface offlineIMService = null;
            string profileServiceURI = string.Empty;

            /* check if it is standalone */
#if DEBUG
            if (StandaloneServices != null)
            {
                m_Log.InfoFormat("Agent {0} has HomeURI {1} compare to standalone HomeURI {2}", authData.AccountInfo.Principal.FullName, authData.AccountInfo.Principal.HomeURI.ToString(), m_HomeURI);
            }
#endif
            if (authData.AccountInfo.Principal.HomeURI.ToString().StartsWith(m_HomeURI, true, CultureInfo.InvariantCulture) && StandaloneServices != null)
            {
                m_Log.InfoFormat("Agent {0} is local to us", authData.AccountInfo.Principal.FullName);
                assetService = StandaloneServices.AssetService;
                inventoryService = StandaloneServices.InventoryService;
                profileService = StandaloneServices.ProfileService;
                friendsService = StandaloneServices.FriendsService;
                offlineIMService = StandaloneServices.OfflineIMService;
            }
            else
            {
                GridParameterMap gridparams = FindGridParameterMap(authData.AccountInfo.Principal.HomeURI.ToString(), scene);
                if (gridparams != null)
                {
                    assetServerURI = gridparams.AssetServerURI;
                    inventoryServerURI = gridparams.InventoryServerURI;
                    if (!string.IsNullOrEmpty(gridparams.GatekeeperURI))
                    {
                        gatekeeperURI = gridparams.GatekeeperURI;
                    }
                    if (!string.IsNullOrEmpty(gridparams.GridUserServerURI))
                    {
                        gridUserService = new RobustGridUserConnector(gridparams.GridUserServerURI);
                    }
                    if (!string.IsNullOrEmpty(gridparams.PresenceServerURI))
                    {
                        presenceService = new RobustPresenceConnector(gridparams.PresenceServerURI, authData.AccountInfo.Principal.HomeURI.ToString());
                    }
                    if (!string.IsNullOrEmpty(gridparams.OfflineIMServerURI))
                    {
                        offlineIMService = new RobustOfflineIMConnector(gridparams.OfflineIMServerURI);
                    }
                    if (!string.IsNullOrEmpty(gridparams.FriendsServerURI))
                    {
                        friendsService = new RobustFriendsConnector(gridparams.FriendsServerURI, gridparams.HomeURI);
                    }
                }
                else
                {
                    presenceService = string.IsNullOrEmpty(m_DefaultPresenceServerURI) ?
                        (PresenceServiceInterface)new RobustHGOnlyPresenceConnector(authData.AccountInfo.Principal.HomeURI.ToString()) :
                        new RobustHGPresenceConnector(m_DefaultPresenceServerURI, authData.AccountInfo.Principal.HomeURI.ToString());
                }
            }

            if (gridUserService == null)
            {
                gridUserService = m_GridUserService;
            }
            userAgentService = new RobustUserAgentConnector(authData.AccountInfo.Principal.HomeURI.ToString());

            if (!authData.AccountInfo.ServiceURLs.TryGetValue("ProfileServerURI", out profileServiceURI))
            {
                profileServiceURI = string.Empty;
            }

            if (!string.IsNullOrEmpty(profileServiceURI))
            {
                string profileType = HeloRequester(profileServiceURI);
                if (m_ProfileServicePlugins.ContainsKey(profileType))
                {
                    profileService = m_ProfileServicePlugins[profileType].Instantiate(profileServiceURI);
                }
            }

            IAgent sceneAgent;
            if (scene.Agents.TryGetValue(authData.AccountInfo.Principal.ID, out sceneAgent))
            {
                if (sceneAgent.Owner.EqualsGrid(authData.AccountInfo.Principal))
                {
                    if (circuitInfo.IsChild && !sceneAgent.IsInScene(scene))
                    {
                        /* already got an agent here */
                        m_Log.WarnFormat("Failed to create agent due to duplicate agent id. {0} != {1}", sceneAgent.Owner.ToString(), authData.AccountInfo.Principal.ToString());
                        throw new OpenSimTeleportProtocol.TeleportFailedException("Failed to create agent due to duplicate agent id");
                    }
                    else if (!circuitInfo.IsChild && !sceneAgent.IsInScene(scene))
                    {
                        /* child becomes root */
                        throw new OpenSimTeleportProtocol.TeleportFailedException("Teleport destination not yet implemented");
                    }
                }
                else if (sceneAgent.Owner.ID == authData.AccountInfo.Principal.ID)
                {
                    /* we got an agent already and no grid match? */
                    m_Log.WarnFormat("Failed to create agent due to duplicate agent id. {0} != {1}", sceneAgent.Owner.ToString(), authData.AccountInfo.Principal.ToString());
                    throw new OpenSimTeleportProtocol.TeleportFailedException("Failed to create agent due to duplicate agent id");
                }
            }

            GroupsServiceInterface groupsService = null;

            if (assetService == null)
            {
                string assetType = HeloRequester(assetServerURI);
                assetService = (string.IsNullOrEmpty(assetType) || assetType == "opensim-robust") ?
                    new RobustAssetConnector(assetServerURI) :
                    m_AssetServicePlugins[assetType].Instantiate(assetServerURI);
            }

            if (inventoryService == null)
            {
                string inventoryType = HeloRequester(inventoryServerURI);
                inventoryService = (string.IsNullOrEmpty(inventoryType) || inventoryType == "opensim-robust") ?
                    new RobustInventoryConnector(inventoryServerURI, groupsService) :
                    m_InventoryServicePlugins[inventoryType].Instantiate(inventoryServerURI);
            }

            GridServiceInterface gridService = scene.GridService;

            var serviceList = new AgentServiceList
            {
                assetService,
                inventoryService,
                groupsService,
                profileService,
                friendsService,
                userAgentService,
                presenceService,
                gridUserService,
                gridService,
                offlineIMService,
                new OpenSimTeleportProtocol(
                Commands,
                m_CapsRedirector,
                m_PacketHandlerPlugins,
                Scenes)
            };
            var agent = new ViewerAgent(
                Scenes,
                authData.AccountInfo.Principal.ID,
                authData.AccountInfo.Principal.FirstName,
                authData.AccountInfo.Principal.LastName,
                authData.AccountInfo.Principal.HomeURI,
                authData.SessionInfo.SessionID,
                authData.SessionInfo.SecureSessionID,
                authData.SessionInfo.ServiceSessionID,
                authData.ClientInfo,
                authData.AccountInfo,
                serviceList)
            {
                ServiceURLs = authData.AccountInfo.ServiceURLs,

                Appearance = authData.AppearanceInfo
            };
            try
            {
                scene.DetermineInitialAgentLocation(agent, authData.DestinationInfo.TeleportFlags, authData.DestinationInfo.Location, authData.DestinationInfo.LookAt);
            }
            catch (Exception e)
            {
                m_Log.InfoFormat("Failed to determine initial location for agent {0}: {1}: {2}", authData.AccountInfo.Principal.FullName, e.GetType().FullName, e.Message);
#if DEBUG
                m_Log.Debug("Exception", e);
#endif
                throw new OpenSimTeleportProtocol.TeleportFailedException(e.Message);
            }

            var udpServer = (UDPCircuitsManager)scene.UDPServer;

            IPAddress ipAddr;
            if (!IPAddress.TryParse(authData.ClientInfo.ClientIP, out ipAddr))
            {
                m_Log.InfoFormat("Invalid IP address for agent {0}", authData.AccountInfo.Principal.FullName);
                throw new OpenSimTeleportProtocol.TeleportFailedException("Invalid IP address");
            }
            var ep = new IPEndPoint(ipAddr, 0);
            var circuit = new AgentCircuit(
                Commands,
                agent,
                udpServer,
                circuitInfo.CircuitCode,
                m_CapsRedirector,
                circuitInfo.CapsPath,
                agent.ServiceURLs,
                gatekeeperURI,
                m_PacketHandlerPlugins,
                ep)
            {
                LastTeleportFlags = authData.DestinationInfo.TeleportFlags,
                Agent = agent,
                AgentID = authData.AccountInfo.Principal.ID,
                SessionID = authData.SessionInfo.SessionID
            };
            agent.Circuits.Add(circuit.Scene.ID, circuit);

            try
            {
                scene.Add(agent);
                try
                {
                    udpServer.AddCircuit(circuit);
                }
                catch(Exception e)
                {
                    m_Log.Debug("Failed adding circuit", e);
                    scene.Remove(agent);
                    throw;
                }
            }
            catch (Exception e)
            {
                m_Log.Debug("Failed agent post", e);
                agent.Circuits.Clear();
                throw new OpenSimTeleportProtocol.TeleportFailedException(e.Message);
            }

            try
            {
                agent.EconomyService?.Login(authData.DestinationInfo.ID, authData.AccountInfo.Principal, authData.SessionInfo.SessionID, authData.SessionInfo.SecureSessionID);
            }
            catch (Exception e)
            {
                m_Log.Warn("Could not contact EconomyService", e);
            }

            if (!circuitInfo.IsChild)
            {
                /* make agent a root agent */
                agent.SceneID = scene.ID;
                if (gridUserService != null)
                {
                    try
                    {
                        gridUserService.SetPosition(agent.Owner, scene.ID, agent.GlobalPosition, agent.LookAt);
                    }
                    catch (Exception e)
                    {
                        m_Log.Warn("Could not contact GridUserService", e);
                    }
                }
            }

            try
            {
                presenceService.Report(new PresenceInfo
                {
                    UserID = agent.Owner,
                    SessionID = agent.SessionID,
                    SecureSessionID = authData.SessionInfo.SecureSessionID,
                    RegionID = scene.ID
                });
            }
            catch (Exception e)
            {
                m_Log.Warn("Could not contact PresenceService", e);
            }
            circuit.LogIncomingAgent(m_Log, circuitInfo.IsChild);
        }
    }
}
