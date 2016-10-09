// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.OpenSim.Teleport;
using SilverSim.BackendConnectors.Robust.Asset;
using SilverSim.BackendConnectors.Robust.Friends;
using SilverSim.BackendConnectors.Robust.GridUser;
using SilverSim.BackendConnectors.Robust.IM;
using SilverSim.BackendConnectors.Robust.Inventory;
using SilverSim.BackendConnectors.Robust.Presence;
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
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
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.GridUser;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.ServiceInterfaces.Profile;
using SilverSim.ServiceInterfaces.ServerParam;
using SilverSim.ServiceInterfaces.UserAgents;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Agent;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Groups;
using SilverSim.Types.Presence;
using SilverSim.Types.StructuredData.Json;
using SilverSim.Viewer.Core;
using SilverSim.Viewer.Messages.Agent;
using SilverSim.Viewer.Messages.Circuit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace SilverSim.BackendHandlers.Robust.Simulation
{
    #region Service Implementation
    [Description("OpenSim PostAgent Handler")]
    [ServerParam("OpenSimProtocolCompatibility")]
    public class PostAgentHandler : IPlugin, IPluginShutdown, IServerParamListener
    {
        /* CAUTION! Never ever make a protocol version configurable */
        const string PROTOCOL_VERSION = "SIMULATION/0.3";
        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST AGENT HANDLER");
        private BaseHttpServer m_HttpServer;
        private Main.Common.Caps.CapsHttpRedirector m_CapsRedirector;
        readonly string m_DefaultGridUserServerURI = string.Empty;
        readonly string m_DefaultPresenceServerURI = string.Empty;
        readonly Dictionary<string, IAssetServicePlugin> m_AssetServicePlugins = new Dictionary<string, IAssetServicePlugin>();
        readonly Dictionary<string, IInventoryServicePlugin> m_InventoryServicePlugins = new Dictionary<string, IInventoryServicePlugin>();
        readonly Dictionary<string, IProfileServicePlugin> m_ProfileServicePlugins = new Dictionary<string, IProfileServicePlugin>();
        List<IProtocolExtender> m_PacketHandlerPlugins = new List<IProtocolExtender>();
        List<AuthorizationServiceInterface> m_AuthorizationServices;
        protected SceneList m_Scenes { get; private set; }
        protected CommandRegistry m_Commands { get; private set; }

        sealed class GridParameterMap : ICloneable
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

            public GridParameterMap()
            {

            }

            public object Clone()
            {
                GridParameterMap m = new GridParameterMap();
                m.HomeURI = HomeURI;
                m.GatekeeperURI = GatekeeperURI;
                m.AssetServerURI = AssetServerURI;
                m.InventoryServerURI = InventoryServerURI;
                m.GridUserServerURI = GridUserServerURI;
                m.PresenceServerURI = PresenceServerURI;
                m.AvatarServerURI = AvatarServerURI;
                m.FriendsServerURI = FriendsServerURI;
                m.ProfileServerURI = ProfileServerURI;
                m.OfflineIMServerURI = OfflineIMServerURI;
                m.ValidForSims.AddRange(ValidForSims);
                return m;
            }
        }

        readonly RwLockedList<GridParameterMap> m_GridParameterMap = new RwLockedList<GridParameterMap>();

        readonly string m_AgentBaseURL = "/agent/";

        readonly RwLockedDictionary<UUID, bool> m_OpenSimProtocolCompatibilityParams = new RwLockedDictionary<UUID, bool>();

        [ServerParam("OpenSimProtocolCompatibility")]
        public void OpenSimProtocolCompatibilityUpdated(UUID regionId, string value)
        {
            bool boolval;
            if(value == string.Empty)
            {
                m_OpenSimProtocolCompatibilityParams.Remove(regionId);
            }
            else if(bool.TryParse(value, out boolval))
            {
                m_OpenSimProtocolCompatibilityParams[regionId] = boolval;
            }
            else
            {
                m_OpenSimProtocolCompatibilityParams[regionId] = false;
            }
        }

        bool GetOpenSimProtocolCompatibility(UUID regionId)
        {
            bool boolval;
            if(m_OpenSimProtocolCompatibilityParams.TryGetValue(regionId, out boolval))
            {
                return boolval;
            }
            else if(m_OpenSimProtocolCompatibilityParams.TryGetValue(UUID.Zero, out boolval))
            {
                return boolval;
            }
            else
            {
                return false;
            }
        }

        public PostAgentHandler(IConfig ownSection)
        {
            m_DefaultGridUserServerURI = ownSection.GetString("DefaultGridUserServerURI", string.Empty);
            m_DefaultPresenceServerURI = ownSection.GetString("DefaultPresenceServerURI", string.Empty);
        }

        protected PostAgentHandler(string agentBaseURL, IConfig ownSection)
        {
            m_AgentBaseURL = agentBaseURL;
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
                if(uri.Contains("?"))
                {
                    uri = uri.Substring(0, uri.IndexOf('?'));
                }
                uri = uri.TrimEnd('/') + "/helo/";
            }

            Dictionary<string, string> headers = new Dictionary<string, string>();
            try
            {
                using (Stream responseStream = HttpRequestHandler.DoStreamRequest("HEAD", uri, null, string.Empty, string.Empty, false, 20000, headers))
                {
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        reader.ReadToEnd();
                    }
                }

                if (!headers.ContainsKey("X-Handlers-Provided"))
                {
                    return "opensim-robust"; /* let us assume Robust API */
                }
                return headers["X-Handlers-Provided"];
            }
            catch
            {
                return "opensim-robust"; /* let us assume Robust API */
            }
        }

        public virtual void Startup(ConfigurationLoader loader)
        {
            m_Scenes = loader.Scenes;
            m_Commands = loader.CommandRegistry;
            m_Log.Info("Initializing agent post handler for " + m_AgentBaseURL);
            m_AuthorizationServices = loader.GetServicesByValue<AuthorizationServiceInterface>();
            m_HttpServer = loader.HttpServer;
            m_HttpServer.StartsWithUriHandlers.Add(m_AgentBaseURL, AgentPostHandler);
            foreach(IAssetServicePlugin plugin in loader.GetServicesByValue<IAssetServicePlugin>())
            {
                m_AssetServicePlugins.Add(plugin.Name, plugin);
            }
            foreach(IInventoryServicePlugin plugin in loader.GetServicesByValue<IInventoryServicePlugin>())
            {
                m_InventoryServicePlugins.Add(plugin.Name, plugin);
            }
            foreach (IProfileServicePlugin plugin in loader.GetServicesByValue<IProfileServicePlugin>())
            {
                m_ProfileServicePlugins.Add(plugin.Name, plugin);
            }

            m_PacketHandlerPlugins = loader.GetServicesByValue<IProtocolExtender>();

            foreach(IConfig section in loader.Config.Configs)
            {
                if(section.Name.StartsWith("RobustGrid-"))
                {
                    if(!section.Contains("HomeURI") || !section.Contains("AssetServerURI") || !section.Contains("InventoryServerURI"))
                    {
                        m_Log.WarnFormat("Skipping section {0} for missing entries (HomeURI, AssetServerURI and InventoryServerURI are required)", section.Name);
                        continue;
                    }
                    GridParameterMap map = new GridParameterMap();
                    map.HomeURI = section.GetString("HomeURI");
                    map.AssetServerURI = section.GetString("AssetServerURI");
                    map.GridUserServerURI = section.GetString("GridUserServerURI", m_DefaultGridUserServerURI);
                    map.PresenceServerURI = section.GetString("PresenceServerURI", string.Empty);
                    map.AvatarServerURI = section.GetString("AvatarServerURI", string.Empty);
                    map.InventoryServerURI = section.GetString("InventoryServerURI");
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
                    else if(map.OfflineIMServerURI.Length != 0 && !Uri.IsWellFormedUriString(map.OfflineIMServerURI, UriKind.Absolute))
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

                    if(section.Contains("ValidFor"))
                    {
                        string[] sims = section.GetString("ValidFor", string.Empty).Split(new char[]{','}, StringSplitOptions.RemoveEmptyEntries);
                        if(sims.Length != 0)
                        {
                            foreach(string sim in sims)
                            {
                                UUID id;
                                if(!UUID.TryParse(sim.Trim(), out id))
                                {
                                    m_Log.ErrorFormat("Invalid UUID {0} encountered within ValidFor in section {1}", sim, section.Name);
                                    continue;
                                }
                                map.ValidForSims.Add(id);
                            }
                            if(map.ValidForSims.Count == 0)
                            {
                                m_Log.WarnFormat("Grid Parameter Map section {0} will be valid for all sims", section.Name);
                            }
                        }
                    }
                    m_GridParameterMap.Add(map);
                    foreach(string key in section.GetKeys())
                    {
                        if(key.StartsWith("Alias-"))
                        {
                            GridParameterMap map2 = (GridParameterMap)map.Clone();
                            map2.HomeURI = section.GetString(key);
                            m_GridParameterMap.Add(map2);
                        }
                    }
                }
            }

            m_CapsRedirector = loader.GetService<Main.Common.Caps.CapsHttpRedirector>("CapsRedirector");
        }

        public ShutdownOrder ShutdownOrder
        {
            get
            {
                return ShutdownOrder.Any;
            }
        }

        public virtual void Shutdown()
        {
            m_HttpServer.StartsWithUriHandlers.Remove(m_AgentBaseURL);
        }

        private void GetAgentParams(string uri, out UUID agentID, out UUID regionID, out string action)
        {
            agentID = UUID.Zero;
            regionID = UUID.Zero;
            action = string.Empty;

            uri = uri.Trim(new char[] { '/' });
            string[] parts = uri.Split('/');
            if(parts.Length < 2)
            {
                throw new InvalidDataException();
            }
            else
            {
                if(!UUID.TryParse(parts[1], out agentID))
                {
                    throw new InvalidDataException();
                }
                if(parts.Length > 2 &&
                    !UUID.TryParse(parts[2], out regionID))
                {
                    throw new InvalidDataException();
                }
                if(parts.Length > 3)
                {
                    action = parts[3];
                }
            }
        }

        GridParameterMap FindGridParameterMap(string homeURI, SceneInterface scene)
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

        public void DoAgentResponse(HttpRequest req, string reason, bool success)
        {
            Map resmap = new Map();
            resmap.Add("reason", reason);
            resmap.Add("success", success);
            resmap.Add("your_ip", req.CallerIP);
            using (HttpResponse res = req.BeginResponse())
            {
                res.ContentType = "application/json";
                using (Stream o = res.GetOutputStream())
                {
                    Json.Serialize(resmap, o);
                }
            }
        }

        protected virtual void CheckScenePerms(UUID sceneID)
        {

        }

        void AgentPostHandler(HttpRequest req)
        {
            if (req.Method == "POST")
            {
                AgentPostHandler_POST(req);
            }
            else if (req.Method == "PUT")
            {
                AgentPostHandler_PUT(req);
            }
            else if (req.Method == "DELETE")
            {
                AgentPostHandler_DELETE(req);
            }
            else if (req.Method == "QUERYACCESS")
            {
                AgentPostHandler_QUERYACCESS(req);
            }
            else
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed");
            }
        }

        void AgentPostHandler_POST(HttpRequest req)
        {
            UUID agentID;
            UUID regionID;
            string action;
            try
            {
                GetAgentParams(req.RawUrl, out agentID, out regionID, out action);
            }
            catch (Exception e)
            {
                m_Log.InfoFormat("Invalid parameters for agent message {0}", req.RawUrl);
                req.ErrorResponse(HttpStatusCode.NotFound, e.Message);
                return;
            }

            using (Stream httpBody = req.Body)
            {
                if (req.ContentType == "application/x-gzip")
                {
                    using(Stream gzHttpBody = new GZipStream(httpBody, CompressionMode.Decompress))
                    {
                        AgentPostHandler_POST(req, gzHttpBody, agentID, regionID, action);
                    }
                }
                else if (req.ContentType == "application/json")
                {
                    AgentPostHandler_POST(req, httpBody, agentID, regionID, action);
                }
                else
                {
                    m_Log.InfoFormat("Invalid content for agent message {0}: {1}", req.RawUrl, req.ContentType);
                    req.ErrorResponse(HttpStatusCode.BadRequest, "Invalid content for agent message");
                    return;
                }
            }
        }

        void AgentPostHandler_POST(HttpRequest req, Stream httpBody, UUID agentID, UUID regionID, string action)
        {
            PostData agentPost;

            try
            {
                agentPost = PostData.Deserialize(httpBody);
            }
            catch (Exception e)
            {
                m_Log.InfoFormat("Deserialization error for agent message {0}: {1}: {2}\n{3}", req.RawUrl, e.GetType().FullName, e.Message, e.StackTrace);
                req.ErrorResponse(HttpStatusCode.BadRequest, e.Message);
                return;
            }

            SceneInterface scene;
            if (!m_Scenes.TryGetValue(agentPost.Destination.ID, out scene))
            {
                m_Log.InfoFormat("No destination for agent {0}", req.RawUrl);
                req.ErrorResponse(HttpStatusCode.NotFound, "Not Found");
                return;
            }

            try
            {
                CheckScenePerms(agentPost.Destination.ID);
            }
            catch
            {
                m_Log.InfoFormat("No destination for agent {0}", req.RawUrl);
                req.ErrorResponse(HttpStatusCode.NotFound, "Not Found");
                return;
            }

            string assetServerURI = agentPost.Account.ServiceURLs["AssetServerURI"];
            string inventoryServerURI = agentPost.Account.ServiceURLs["InventoryServerURI"];
            string gatekeeperURI = scene.GatekeeperURI;

            ProfileServiceInterface profileService = null;
            UserAgentServiceInterface userAgentService;
            PresenceServiceInterface presenceService = null;
            GridUserServiceInterface gridUserService = null;
            FriendsServiceInterface friendsService = null;
            OfflineIMServiceInterface offlineIMService = null;
            string profileServiceURI = string.Empty;

            GridParameterMap gridparams = FindGridParameterMap(agentPost.Account.Principal.HomeURI.ToString(), scene);
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
                    presenceService = new RobustPresenceConnector(gridparams.PresenceServerURI, agentPost.Account.Principal.HomeURI.ToString());
                }
                if (!string.IsNullOrEmpty(gridparams.OfflineIMServerURI))
                {
                    offlineIMService = new RobustOfflineIMConnector(gridparams.OfflineIMServerURI);
                }
                if(!string.IsNullOrEmpty(gridparams.FriendsServerURI))
                {
                    friendsService = new RobustFriendsConnector(gridparams.FriendsServerURI, gridparams.HomeURI);
                }
            }
            else
            {
                presenceService = string.IsNullOrEmpty(m_DefaultPresenceServerURI) ?
                    (PresenceServiceInterface)new RobustHGOnlyPresenceConnector(agentPost.Account.Principal.HomeURI.ToString()) :
                    new RobustHGPresenceConnector(m_DefaultPresenceServerURI, agentPost.Account.Principal.HomeURI.ToString());
            }
            userAgentService = new RobustUserAgentConnector(agentPost.Account.Principal.HomeURI.ToString());

            if (agentPost.Account.ServiceURLs.ContainsKey("ProfileServerURI"))
            {
                profileServiceURI = agentPost.Account.ServiceURLs["ProfileServerURI"];
            }

            if (!string.IsNullOrEmpty(profileServiceURI))
            {
                string profileType = HeloRequester(profileServiceURI);
                if (m_ProfileServicePlugins.ContainsKey(profileType))
                {
                    profileService = m_ProfileServicePlugins[profileType].Instantiate(profileServiceURI);
                }
            }

            if (!string.IsNullOrEmpty(agentPost.Session.ServiceSessionID) || !GetOpenSimProtocolCompatibility(agentPost.Destination.ID))
            {
                try
                {
                    userAgentService.VerifyAgent(agentPost.Session.SessionID, agentPost.Session.ServiceSessionID);
                }
                catch
#if DEBUG
                (Exception e)
#endif
                {
                    m_Log.InfoFormat("Failed to verify agent {0} at Home Grid (Code 1)", agentPost.Account.Principal.FullName);
                    DoAgentResponse(req, "Failed to verify agent at Home Grid (Code 1)", false);
                    return;
                }
            }
            else
            {
                m_Log.WarnFormat("OpenSim protocol in use for agent {0}.", agentPost.Account.Principal.FullName);
            }

            try
            {
                userAgentService.VerifyClient(agentPost.Session.SessionID, agentPost.Client.ClientIP);
            }
            catch
#if DEBUG
                (Exception e)
#endif
            {
                m_Log.InfoFormat("Failed to verify client {0} at Home Grid (Code 2)", agentPost.Account.Principal.FullName);
                DoAgentResponse(req, "Failed to verify client at Home Grid (Code 2)", false);
                return;
            }

            /* We have established trust of home grid by verifying its agent. 
             * At least agent and grid belong together.
             * 
             * Now, we can validate the access of the agent.
             */
            AuthorizationServiceInterface.AuthorizationData ad = new AuthorizationServiceInterface.AuthorizationData();
            ad.ClientInfo = agentPost.Client;
            ad.SessionInfo = agentPost.Session;
            ad.AccountInfo = agentPost.Account;
            ad.DestinationInfo = agentPost.Destination;
            ad.AppearanceInfo = agentPost.Appearance;

            try
            {
                foreach (AuthorizationServiceInterface authService in m_AuthorizationServices)
                {
                    authService.Authorize(ad);
                }
            }
            catch (AuthorizationServiceInterface.NotAuthorizedException e)
            {
                DoAgentResponse(req, e.Message, false);
                return;
            }
            catch (Exception e)
            {
                DoAgentResponse(req, "Failed to verify client's authorization at destination", false);
                m_Log.Warn("Failed to verify agent's authorization at destination.", e);
                return;
            }

            try
            {
                IAgent sceneAgent = scene.Agents[agentPost.Account.Principal.ID];
                if (sceneAgent.Owner.EqualsGrid(agentPost.Account.Principal))
                {
                    if (agentPost.Circuit.IsChild && !sceneAgent.IsInScene(scene))
                    {
                        /* already got an agent here */
                        DoAgentResponse(req, "Failed to create agent due to duplicate agent id", false);
                        m_Log.WarnFormat("Failed to create agent due to duplicate agent id. {0} != {1}", sceneAgent.Owner.ToString(), agentPost.Account.Principal.ToString());
                        return;
                    }
                    else if (!agentPost.Circuit.IsChild && !sceneAgent.IsInScene(scene))
                    {
                        /* child becomes root */
                        DoAgentResponse(req, "Teleport destination not yet implemented", false);
                        return;
                    }
                }
                else if (sceneAgent.Owner.ID == agentPost.Account.Principal.ID)
                {
                    /* we got an agent already and no grid match? */
                    DoAgentResponse(req, "Failed to create agent due to duplicate agent id", false);
                    m_Log.WarnFormat("Failed to create agent due to duplicate agent id. {0} != {1}", sceneAgent.Owner.ToString(), agentPost.Account.Principal.ToString());
                    return;
                }
            }
            catch
            {
                /* no action needed */
            }

            GroupsServiceInterface groupsService = null;
            AssetServiceInterface assetService;
            InventoryServiceInterface inventoryService;
            string inventoryType = HeloRequester(inventoryServerURI);
            string assetType = HeloRequester(assetServerURI);

            assetService = (string.IsNullOrEmpty(assetType) || assetType == "opensim-robust") ?
                (AssetServiceInterface)new RobustAssetConnector(assetServerURI) :
                m_AssetServicePlugins[assetType].Instantiate(assetServerURI);

            inventoryService = (string.IsNullOrEmpty(inventoryType) || inventoryType == "opensim-robust") ?
                new RobustInventoryConnector(inventoryServerURI, groupsService) :
                m_InventoryServicePlugins[assetType].Instantiate(inventoryServerURI);

            GridServiceInterface gridService = scene.GridService;

            AgentServiceList serviceList = new AgentServiceList();
            serviceList.Add(assetService);
            serviceList.Add(inventoryService);
            serviceList.Add(groupsService);
            serviceList.Add(profileService);
            serviceList.Add(friendsService);
            serviceList.Add(userAgentService);
            serviceList.Add(presenceService);
            serviceList.Add(gridUserService);
            serviceList.Add(gridService);
            serviceList.Add(offlineIMService);
            serviceList.Add(new OpenSimTeleportProtocol(
                m_Commands,
                m_CapsRedirector,
                m_PacketHandlerPlugins,
                m_Scenes));

            ViewerAgent agent = new ViewerAgent(
                m_Scenes,
                agentPost.Account.Principal.ID,
                agentPost.Account.Principal.FirstName,
                agentPost.Account.Principal.LastName,
                agentPost.Account.Principal.HomeURI,
                agentPost.Session.SessionID,
                agentPost.Session.SecureSessionID,
                agentPost.Session.ServiceSessionID,
                agentPost.Client,
                agentPost.Account,
                serviceList);
            agent.ServiceURLs = agentPost.Account.ServiceURLs;

            agent.Appearance = agentPost.Appearance;
            try
            {
                scene.DetermineInitialAgentLocation(agent, agentPost.Destination.TeleportFlags, agentPost.Destination.Location, agentPost.Destination.LookAt);
            }
            catch(Exception e)
            {
                m_Log.InfoFormat("Failed to determine initial location for agent {0}: {1}: {2}", agentPost.Account.Principal.FullName, e.GetType().FullName, e.Message);
                DoAgentResponse(req, e.Message, false);
                return;
            }

            UDPCircuitsManager udpServer = (UDPCircuitsManager)scene.UDPServer;

            IPAddress ipAddr;
            if (!IPAddress.TryParse(agentPost.Client.ClientIP, out ipAddr))
            {
                m_Log.InfoFormat("Invalid IP address for agent {0}", agentPost.Account.Principal.FullName);
                DoAgentResponse(req, "Invalid IP address", false);
                return;
            }
            IPEndPoint ep = new IPEndPoint(ipAddr, 0);
            AgentCircuit circuit = new AgentCircuit(
                m_Commands,
                agent,
                udpServer,
                agentPost.Circuit.CircuitCode,
                m_CapsRedirector,
                agentPost.Circuit.CapsPath,
                agent.ServiceURLs,
                gatekeeperURI,
                m_PacketHandlerPlugins,
                ep);
            circuit.LastTeleportFlags = agentPost.Destination.TeleportFlags;
            circuit.Agent = agent;
            circuit.AgentID = agentPost.Account.Principal.ID;
            circuit.SessionID = agentPost.Session.SessionID;
            agent.Circuits.Add(circuit.Scene.ID, circuit);

            try
            {
                scene.Add(agent);
                try
                {
                    udpServer.AddCircuit(circuit);
                }
                catch
                {
                    scene.Remove(agent);
                    throw;
                }
            }
            catch (Exception e)
            {
                m_Log.Debug("Failed agent post", e);
                agent.Circuits.Clear();
                DoAgentResponse(req, e.Message, false);
                return;
            }
            if (!agentPost.Circuit.IsChild)
            {
                /* make agent a root agent */
                agent.SceneID = scene.ID;
                if (null != gridUserService)
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
                PresenceInfo pinfo = new PresenceInfo();
                pinfo.UserID = agent.Owner;
                pinfo.SessionID = agent.SessionID;
                pinfo.SecureSessionID = agentPost.Session.SecureSessionID;
                pinfo.RegionID = scene.ID;
                presenceService[agent.SessionID, agent.ID, PresenceServiceInterface.SetType.Report] = pinfo;
            }
            catch (Exception e)
            {
                m_Log.Warn("Could not contact PresenceService", e);
            }
            circuit.LogIncomingAgent(m_Log, agentPost.Circuit.IsChild);
            DoAgentResponse(req, "authorized", true);
        }

        void AgentPostHandler_PUT(HttpRequest req)
        {
            UUID agentID;
            UUID regionID;
            string action;
            try
            {
                GetAgentParams(req.RawUrl, out agentID, out regionID, out action);
            }
            catch (Exception e)
            {
                m_Log.InfoFormat("Invalid parameters for agent message {0}", req.RawUrl);
                req.ErrorResponse(HttpStatusCode.NotFound, e.Message);
                return;
            }

            /* this is the rather nasty HTTP variant of the UDP AgentPosition messaging */
            using (Stream httpBody = req.Body)
            {
                if (req.ContentType == "application/x-gzip")
                {
                    using(Stream gzHttpBody = new GZipStream(httpBody, CompressionMode.Decompress))
                    {
                        AgentPostHandler_PUT_Inner(req, gzHttpBody, agentID, regionID, action);
                    }
                }
                else if (req.ContentType == "application/json")
                {
                    AgentPostHandler_PUT_Inner(req, httpBody, agentID, regionID, action);
                }
                else
                {
                    m_Log.InfoFormat("Invalid content for agent message {0}: {1}", req.RawUrl, req.ContentType);
                    req.ErrorResponse(HttpStatusCode.UnsupportedMediaType, "Invalid content for agent message");
                    return;
                }
            }
        }
         
        void AgentPostHandler_PUT_Inner(HttpRequest req, Stream httpBody, UUID agentID, UUID regionID, string action)
        {
            IValue json;

            try
            {
                json = Json.Deserialize(httpBody);
            }
            catch (Exception e)
            {
                m_Log.InfoFormat("Deserialization error for agent message {0}\n{1}", req.RawUrl, e.StackTrace);
                req.ErrorResponse(HttpStatusCode.BadRequest, e.Message);
                return;
            }

            Map param = (Map)json;
            string msgType;
            msgType = param.ContainsKey("message_type") ? param["message_type"].ToString() : "AgentData";
            if (msgType == "AgentData")
            {
                AgentPostHandler_PUT_AgentData(req, agentID, regionID, action, param);
            }
            else if (msgType == "AgentPosition")
            {
                AgentPostHandler_PUT_AgentPosition(req, agentID, regionID, action, param);
            }
            else
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Unknown message type");
            }
        }

        void AgentPostHandler_PUT_AgentData(HttpRequest req, UUID agentID, UUID regionID, string action, Map param)
        {
            ChildAgentUpdate childAgentData = new ChildAgentUpdate();

            UUID destinationRegionID = param["destination_uuid"].AsUUID;

            childAgentData.RegionID = param["region_id"].AsUUID;
            childAgentData.ViewerCircuitCode = param["circuit_code"].AsUInt;
            childAgentData.AgentID = param["agent_uuid"].AsUUID;
            childAgentData.SessionID = param["session_uuid"].AsUUID;
            if (param.ContainsKey("position"))
            {
                childAgentData.AgentPosition = param["position"].AsVector3;
            }
            if (param.ContainsKey("velocity"))
            {
                childAgentData.AgentVelocity = param["velocity"].AsVector3;
            }
            if (param.ContainsKey("center"))
            {
                childAgentData.Center = param["center"].AsVector3;
            }
            if (param.ContainsKey("size"))
            {
                childAgentData.Size = param["size"].AsVector3;
            }
            if (param.ContainsKey("at_axis"))
            {
                childAgentData.AtAxis = param["at_axis"].AsVector3;
            }
            if (param.ContainsKey("left_axis"))
            {
                childAgentData.LeftAxis = param["left_axis"].AsVector3;
            }
            if (param.ContainsKey("up_axis"))
            {
                childAgentData.UpAxis = param["up_axis"].AsVector3;
            }
            /*


    if (args.ContainsKey("wait_for_root") && args["wait_for_root"] != null)
        SenderWantsToWaitForRoot = args["wait_for_root"].AsBoolean();
             */

            if (param.ContainsKey("far"))
            {
                childAgentData.Far = param["far"].AsReal;
            }
            if (param.ContainsKey("aspect"))
            {
                childAgentData.Aspect = param["aspect"].AsReal;
            }
            //childAgentData.Throttles = param["throttles"];
            childAgentData.LocomotionState = param["locomotion_state"].AsUInt;
            if (param.ContainsKey("head_rotation"))
            {
                childAgentData.HeadRotation = param["head_rotation"].AsQuaternion;
            }
            if (param.ContainsKey("body_rotation"))
            {
                childAgentData.BodyRotation = param["body_rotation"].AsQuaternion;
            }
            if (param.ContainsKey("control_flags"))
            {
                childAgentData.ControlFlags = (ControlFlags)param["control_flags"].AsUInt;
            }
            if (param.ContainsKey("energy_level"))
            {
                childAgentData.EnergyLevel = param["energy_level"].AsReal;
            }
            if (param.ContainsKey("god_level"))
            {
                childAgentData.GodLevel = (byte)param["god_level"].AsUInt;
            }
            if (param.ContainsKey("always_run"))
            {
                childAgentData.AlwaysRun = param["always_run"].AsBoolean;
            }
            if (param.ContainsKey("prey_agent"))
            {
                childAgentData.PreyAgent = param["prey_agent"].AsUUID;
            }
            if (param.ContainsKey("agent_access"))
            {
                childAgentData.AgentAccess = (byte)param["agent_access"].AsUInt;
            }
            if (param.ContainsKey("active_group_id"))
            {
                childAgentData.ActiveGroupID = param["active_group_id"].AsUUID;
            }

            if (param.ContainsKey("groups"))
            {
                AnArray groups = param["groups"] as AnArray;
                if (groups != null)
                {
                    foreach (IValue gval in groups)
                    {
                        Map group = (Map)gval;
                        ChildAgentUpdate.GroupDataEntry g = new ChildAgentUpdate.GroupDataEntry();
                        g.AcceptNotices = group["accept_notices"].AsBoolean;
                        UInt64 groupPowers;
                        if (UInt64.TryParse(group["group_powers"].ToString(), out groupPowers))
                        {
                            g.GroupPowers = (GroupPowers)groupPowers;
                            g.GroupID = group["group_id"].AsUUID;
                            childAgentData.GroupData.Add(g);
                        }
                    }
                }
            }

            if (param.ContainsKey("animations"))
            {
                AnArray anims = param["animations"] as AnArray;
                if (anims != null)
                {
                    foreach (IValue aval in anims)
                    {
                        Map anim = (Map)aval;
                        ChildAgentUpdate.AnimationDataEntry a = new ChildAgentUpdate.AnimationDataEntry();
                        a.Animation = anim["animation"].AsUUID;
                        if (anim.ContainsKey("object_id"))
                        {
                            a.ObjectID = anim["object_id"].AsUUID;
                        }
                        childAgentData.AnimationData.Add(a);
                    }
                }
            }
            /*

    if (args["default_animation"] != null)
    {
        try
        {
            DefaultAnim = new Animation((OSDMap)args["default_animation"]);
        }
        catch
        {
            DefaultAnim = null;
        }
    }

    if (args["animation_state"] != null)
    {
        try
        {
            AnimState = new Animation((OSDMap)args["animation_state"]);
        }
        catch
        {
            AnimState = null;
        }
    }
             * */

            /*-----------------------------------------------------------------*/
            /* Appearance */
            Map appearancePack = (Map)param["packed_appearance"];
            AppearanceInfo Appearance = new AppearanceInfo();
            Appearance.AvatarHeight = appearancePack["height"].AsReal;

            if(appearancePack.ContainsKey("visualparams"))
            {
                AnArray vParams = (AnArray)appearancePack["visualparams"];
                byte[] visualParams = new byte[vParams.Count];

                int i;
                for (i = 0; i < vParams.Count; ++i)
                {
                    visualParams[i] = (byte)vParams[i].AsUInt;
                }
                Appearance.VisualParams = visualParams;
            }

            {
                AnArray texArray = (AnArray)appearancePack["textures"];
                int i;
                for (i = 0; i < AppearanceInfo.AvatarTextureData.TextureCount; ++i)
                {
                    Appearance.AvatarTextures[i] = texArray[i].AsUUID;
                }
            }

            {
                int i;
                uint n;
                AnArray wearables = (AnArray)appearancePack["wearables"];
                for (i = 0; i < (int)WearableType.NumWearables; ++i)
                {
                    AnArray ar;
                    try
                    {
                        ar = (AnArray)wearables[i];
                    }
                    catch
                    {
                        continue;
                    }
                    n = 0;
                    foreach (IValue val in ar)
                    {
                        Map wp = (Map)val;
                        AgentWearables.WearableInfo wi = new AgentWearables.WearableInfo();
                        wi.ItemID = wp["item"].AsUUID;
                        wi.AssetID = wp.ContainsKey("asset") ? wp["asset"].AsUUID : UUID.Zero;
                        WearableType type = (WearableType)i;
                        Appearance.Wearables[type, n++] = wi;
                    }
                }
            }

            {
                foreach (IValue apv in (AnArray)appearancePack["attachments"])
                {
                    Map ap = (Map)apv;
                    uint apid;
                    if (uint.TryParse(ap["point"].ToString(), out apid))
                    {
                        Appearance.Attachments[(AttachmentPoint)apid][ap["item"].AsUUID] = UUID.Zero;
                    }
                }
            }

            if (appearancePack.ContainsKey("serial"))
            {
                Appearance.Serial = appearancePack["serial"].AsUInt;
            }

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

            /*
    if (args["callback_uri"] != null)
        CallbackURI = args["callback_uri"].AsString();
             * */

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

            SceneInterface scene;
            if (m_Scenes.TryGetValue(destinationRegionID, out scene))
            {
                IAgent agent;

                try
                {
                    agent = scene.Agents[childAgentData.AgentID];
                }
                catch
                {
                    using (HttpResponse res = req.BeginResponse())
                    {
                        using (StreamWriter s = res.GetOutputStream().UTF8StreamWriter())
                        {
                            s.Write(false.ToString());
                        }
                    }
                    return;
                }

                bool waitForRoot = param.ContainsKey("wait_for_root") && param["wait_for_root"].AsBoolean;

                if(waitForRoot)
                {
                    req.SetConnectionClose();
                    agent.AddWaitForRoot(scene, AgentPostHandler_PUT_WaitForRoot_HttpResponse, req);
                }

                if(param.ContainsKey("callback_uri"))
                {
                    agent.AddWaitForRoot(scene, AgentPostHandler_PUT_WaitForRoot_CallbackURI, param["callback_uri"].ToString());
                }

                try
                {
                    agent.HandleMessage(childAgentData);
                }
                catch
                {
                    req.ErrorResponse(HttpStatusCode.BadRequest, "Unknown message type");
                    return;
                }

                if (!waitForRoot)
                {
                    string resultStr = true.ToString();
                    byte[] resultBytes = resultStr.ToUTF8Bytes();

                    using (HttpResponse res = req.BeginResponse("text/plain"))
                    {
                        using (Stream s = res.GetOutputStream(resultBytes.Length))
                        {
                            s.Write(resultBytes, 0, resultBytes.Length);
                        }
                    }
                }
                else
                {
                    throw new HttpResponse.DisconnectFromThreadException();
                }
            }
            else
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Scene not found");
            }
        }

        void AgentPostHandler_PUT_WaitForRoot_HttpResponse(object o, bool success)
        {
#if DEBUG
            m_Log.DebugFormat("respond to WaitForRoot PUT agent with {0}", success.ToString());
#endif
            HttpRequest req = (HttpRequest)o;
            try
            {
                string resultStr = success.ToString();
                byte[] resultBytes = resultStr.ToUTF8Bytes();
                using (HttpResponse res = req.BeginResponse("text/plain"))
                {
                    using (Stream s = res.GetOutputStream(resultBytes.Length))
                    {
                        s.Write(resultBytes, 0, resultBytes.Length);
                    }
                }
            }
            catch
            {
                /* we are outside of HTTP Server context so we have to catch */
            }
            try
            {
                req.Close();
            }
            catch
            {
                /* we are outside of HTTP Server context so we have to catch */
            }
        }

        void AgentPostHandler_PUT_WaitForRoot_CallbackURI(object o, bool success)
        {
            if (success)
            {
                try
                {
                    HttpRequestHandler.DoRequest("DELETE", (string)o, null, string.Empty, string.Empty, false, 10000);
                }
                catch(Exception e)
                {
                    /* do not pass the exceptions */
                    m_Log.WarnFormat("Exception encountered when calling CallbackURI: {0}: {1}", e.GetType().FullName, e.Message);
                }
            }
        }

        void AgentPostHandler_PUT_AgentPosition(HttpRequest req, UUID agentID, UUID regionID, string action, Map param)
        {
            ChildAgentPositionUpdate childAgentPosition = new ChildAgentPositionUpdate();
            UUID destinationRegionID = param["destination_uuid"].AsUUID;

            UInt64 regionHandle;
            if (!UInt64.TryParse(param["region_handle"].ToString(), out regionHandle))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Unknown message type");
                return;
            }
            childAgentPosition.RegionLocation.RegionHandle = regionHandle;
            childAgentPosition.ViewerCircuitCode = param["circuit_code"].AsUInt;
            childAgentPosition.AgentID = param["agent_uuid"].AsUUID;
            childAgentPosition.SessionID = param["session_uuid"].AsUUID;
            childAgentPosition.AgentPosition = param["position"].AsVector3;
            childAgentPosition.AgentVelocity = param["velocity"].AsVector3;
            childAgentPosition.Center = param["center"].AsVector3;
            childAgentPosition.Size = param["size"].AsVector3;
            childAgentPosition.AtAxis = param["at_axis"].AsVector3;
            childAgentPosition.LeftAxis = param["left_axis"].AsVector3;
            childAgentPosition.UpAxis = param["up_axis"].AsVector3;
            childAgentPosition.ChangedGrid = param["changed_grid"].AsBoolean;
            /* Far and Throttles are extra in opensim so we have to cope with these on sending */

            SceneInterface scene;
            if (m_Scenes.TryGetValue(destinationRegionID, out scene))
            {
                IAgent agent;
                if (!scene.Agents.TryGetValue(childAgentPosition.AgentID, out agent))
                {
                    using (HttpResponse res = req.BeginResponse())
                    {
                        using (StreamWriter s = res.GetOutputStream().UTF8StreamWriter())
                        {
                            s.Write(false.ToString());
                        }
                    }
                    return;
                }

                try
                {
                    agent.HandleMessage(childAgentPosition);
                    using (HttpResponse res = req.BeginResponse())
                    {
                        using (StreamWriter s = res.GetOutputStream().UTF8StreamWriter())
                        {
                            s.Write(true.ToString());
                        }
                    }
                }
                catch
                {
                    req.ErrorResponse(HttpStatusCode.BadRequest, "Unknown message type");
                    return;
                }
            }
            else
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Scene not found");
            }
        }

        void AgentPostHandler_DELETE(HttpRequest req)
        {
            UUID agentID;
            UUID regionID;
            string action;
            try
            {
                GetAgentParams(req.RawUrl, out agentID, out regionID, out action);
            }
            catch (Exception e)
            {
                m_Log.InfoFormat("Invalid parameters for agent message {0}", req.RawUrl);
                req.ErrorResponse(HttpStatusCode.NotFound, e.Message);
                return;
            }

            SceneInterface scene;
            try
            {
                scene = m_Scenes[regionID];
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.NotFound, "Not Found");
                return;
            }

            IAgent agent;
            try
            {
                agent = scene.Agents[agentID];
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.NotFound, "Not Found");
                return;
            }

            if (action == "release")
            {
                /* map this to the teleport protocol */
                /* it will make the agent become a child */
                IAgentTeleportServiceInterface teleportService = agent.ActiveTeleportService;
                if (null != teleportService)
                {
                    teleportService.ReleaseAgent(scene.ID);
                }
                return;
            }

            if (agent.IsInScene(scene))
            {
                /* we are not killing any root agent here unconditionally */
                /* It is one major design issue within OpenSim not checking that nicely. */
                IAgentTeleportServiceInterface teleportService = agent.ActiveTeleportService;
                if (null != teleportService)
                {
                    /* we give the teleport handler the chance to do everything required */
                    teleportService.CloseAgentOnRelease(scene.ID);
                }
                else
                {
                    req.ErrorResponse(HttpStatusCode.Forbidden, "Forbidden");
                    return;
                }
            }
            else
            {
                /* let the disconnect be handled by Circuit */
                agent.SendMessageAlways(new DisableSimulator(), scene.ID);
            }
            using(HttpResponse res = req.BeginResponse(HttpStatusCode.OK, "OK"))
            {

            }
        }

        void AgentPostHandler_QUERYACCESS(HttpRequest req)
        {
            UUID agentID;
            UUID regionID;
            string action;
            try
            {
                GetAgentParams(req.RawUrl, out agentID, out regionID, out action);
            }
            catch (Exception e)
            {
                m_Log.InfoFormat("Invalid parameters for agent message {0}", req.RawUrl);
                req.ErrorResponse(HttpStatusCode.NotFound, e.Message);
                return;
            }

            Map jsonreq;
            SceneInterface scene;
            try
            {
                scene = m_Scenes[regionID];
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.NotFound, "Not Found");
                return;
            }

            try
            {
                jsonreq = Json.Deserialize(req.Body) as Map;
            }
            catch (Exception e)
            {
                m_Log.InfoFormat("Deserialization error for QUERYACCESS message {0}\n{1}", req.RawUrl, e.StackTrace);
                req.ErrorResponse(HttpStatusCode.BadRequest, e.Message);
                return;
            }

            if (null == jsonreq)
            {
                m_Log.InfoFormat("Deserialization error for QUERYACCESS message {0}", req.RawUrl);
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }

            string myVersion = "SIMULATION/0.3";
            if (jsonreq.ContainsKey("my_version"))
            {
                myVersion = jsonreq["my_version"].ToString();
            }
            string agent_home_uri = null;
            if (jsonreq.ContainsKey("agent_home_uri"))
            {
                agent_home_uri = jsonreq["agent_home_uri"].ToString();
                /* we can only do informal checks here with it.
                 * The agent_home_uri cannot be validated itself.
                 */
            }

            Map response = new Map();
            Map _result = new Map();
            bool success = true;
            string reason = string.Empty;
            string[] myVersionSplit = myVersion.Split(new char[] { '.', '/' });
            if (myVersionSplit.Length < 3)
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }
            if (myVersionSplit[0] != "SIMULATION")
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }
            int versionMajor;
            if(!int.TryParse(myVersionSplit[1], out versionMajor))
            {
                versionMajor = 0;
            }
            int versionMinor;
            if(!int.TryParse(myVersionSplit[2], out versionMinor))
            {
                versionMinor = 0;
            }
            /* check version and limit it down to what we actually understand
             * weird but the truth of OpenSim protocol versioning
             */
            if (versionMajor > 0)
            {
                versionMajor = 0;
                versionMinor = 3;
            }
            if (0 == versionMajor && versionMinor > 3)
            {
                versionMinor = 3;
            }

            if (success && 
                0 == versionMajor && versionMinor < 3 &&
                (scene.SizeX > 256 || scene.SizeY > 256))
            {
                /* check region size 
                 * check both parameters. It seems rectangular vars are not that impossible to have.
                 */
                success = false;
                reason = "Destination is a variable-sized region, and source is an old simulator. Consider upgrading.";
            }

            UUI agentUUI = new UUI();
            agentUUI.ID = agentID;
            if (!string.IsNullOrEmpty(agent_home_uri))
            {
                agentUUI.HomeURI = new Uri(agent_home_uri);
            }

            if (success)
            {
                /* add informational checks only
                 * These provide messages to the incoming agent.
                 * But, the agent info here cannot be validated and therefore
                 * not be trusted.
                 */
                try
                {
                    foreach (AuthorizationServiceInterface authService in m_AuthorizationServices)
                    {
                        authService.QueryAccess(agentUUI, regionID);
                    }
                }
                catch (AuthorizationServiceInterface.NotAuthorizedException e)
                {
                    success = false;
                    reason = e.Message;
                }
                catch (Exception e)
                {
                    /* No one should be able to make any use out of a programming error */
                    success = false;
                    reason = "Internal Error";
                    m_Log.Error("Internal Error", e);
                }
            }

            response.Add("success", success);
            response.Add("reason", reason);
            /* CAUTION! never ever make version parameters a configuration parameter */
            response.Add("version", PROTOCOL_VERSION);
            using(HttpResponse res = req.BeginResponse(HttpStatusCode.OK, "OK"))
            {
                using(Stream s = res.GetOutputStream())
                {
                    Json.Serialize(response, s);
                }
            }
        }
    }
    #endregion

    #region Service Factory
    [PluginName("RobustAgentHandler")]
    public class PostAgentHandlerFactory : IPluginFactory
    {
        public PostAgentHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new PostAgentHandler(ownSection);
        }
    }
    #endregion
}
