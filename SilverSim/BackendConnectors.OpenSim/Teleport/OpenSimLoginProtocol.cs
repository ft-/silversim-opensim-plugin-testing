// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
using SilverSim.BackendConnectors.Robust.UserAgent;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.Caps;
using SilverSim.Main.Common.CmdIO;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.ServiceInterfaces.Teleport;
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
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.Agent;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Grid;
using SilverSim.Types.GridUser;
using SilverSim.Types.Presence;
using SilverSim.Types.StructuredData.Json;
using SilverSim.Viewer.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace SilverSim.BackendConnectors.OpenSim.Teleport
{
    [Description("OpenSim Login Protocol Connector")]
    public class OpenSimLoginProtocol : ILoginConnectorServiceInterface, IPlugin, IServerParamListener
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("OPENSIM LOGIN PROTOCOL");
        GridUserServiceInterface m_GridUserService;
        GridServiceInterface m_GridService;
        BaseHttpServer m_HttpServer;

        readonly bool m_IsStandalone;
        readonly string m_LocalInventoryServiceName;
        InventoryServiceInterface m_LocalInventoryService;
        readonly string m_LocalAssetServiceName;
        AssetServiceInterface m_LocalAssetService;
        readonly string m_LocalProfileServiceName;
        ProfileServiceInterface m_LocalProfileService;
        readonly string m_LocalPresenceServiceName;
        PresenceServiceInterface m_LocalPresenceService;
        readonly string m_LocalFriendsServiceName;
        FriendsServiceInterface m_LocalFriendsService;
        readonly string m_LocalOfflineIMServiceName;
        OfflineIMServiceInterface m_LocalOfflineIMService;
        readonly string m_LocalGroupsServiceName;
        GroupsServiceInterface m_LocalGroupsService;

        List<AuthorizationServiceInterface> m_AuthorizationServices;
        List<IProtocolExtender> m_PacketHandlerPlugins = new List<IProtocolExtender>();
        string m_GatekeeperURI;
        CapsHttpRedirector m_CapsRedirector;
        SceneList m_Scenes;

        readonly string m_GridUserServiceName;
        readonly string m_GridServiceName;

        protected CommandRegistry m_Commands { get; private set; }

        public int TimeoutMs = 30000;

        public OpenSimLoginProtocol(IConfig ownConfig)
        {
            m_GridUserServiceName = ownConfig.GetString("GridUserService", "GridUserService");
            m_GridServiceName = ownConfig.GetString("GridService", "GridService");
            m_IsStandalone = ownConfig.GetBoolean("IsStandalone", false);
            if(m_IsStandalone)
            {
                m_LocalInventoryServiceName = ownConfig.GetString("LocalInventoryService", "InventoryService");
                m_LocalAssetServiceName = ownConfig.GetString("LocalAssetService", "AssetService");
                m_LocalProfileServiceName = ownConfig.GetString("LocalProfileService", "ProfileService");
                m_LocalFriendsServiceName = ownConfig.GetString("LocalFriendsService", "FriendsService");
                m_LocalPresenceServiceName = ownConfig.GetString("LocalPresenceService", "PresenceService");
                m_LocalOfflineIMServiceName = ownConfig.GetString("LocalOfflineIMService", "OfflineIMService");
                m_LocalGroupsServiceName = ownConfig.GetString("LocalGroupsService", string.Empty);
            }
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Scenes = loader.Scenes;
            m_CapsRedirector = loader.CapsRedirector;
            m_AuthorizationServices = loader.GetServicesByValue<AuthorizationServiceInterface>();
            m_HttpServer = loader.HttpServer;
            m_GridUserService = loader.GetService<GridUserServiceInterface>(m_GridUserServiceName);
            m_GridService = loader.GetService<GridServiceInterface>(m_GridServiceName);
            m_Commands = loader.CommandRegistry;
            m_PacketHandlerPlugins = loader.GetServicesByValue<IProtocolExtender>();
            m_GatekeeperURI = loader.GatekeeperURI;

            if (m_IsStandalone)
            {
                m_LocalAssetService = loader.GetService<AssetServiceInterface>(m_LocalAssetServiceName);
                m_LocalInventoryService = loader.GetService<InventoryServiceInterface>(m_LocalInventoryServiceName);
                if (!string.IsNullOrEmpty(m_LocalProfileServiceName))
                {
                    m_LocalProfileService = loader.GetService<ProfileServiceInterface>(m_LocalProfileServiceName);
                }
                m_LocalFriendsService = loader.GetService<FriendsServiceInterface>(m_LocalFriendsServiceName);
                m_LocalPresenceService = loader.GetService<PresenceServiceInterface>(m_LocalPresenceServiceName);
                m_LocalOfflineIMService = loader.GetService<OfflineIMServiceInterface>(m_LocalOfflineIMServiceName);
                if(!string.IsNullOrEmpty(m_LocalGroupsServiceName))
                {
                    m_LocalGroupsService = loader.GetService<GroupsServiceInterface>(m_LocalGroupsServiceName);
                }
            }
        }

        public void LoginTo(UserAccount account, ClientInfo clientInfo, SessionInfo sessionInfo, DestinationInfo destinationInfo, CircuitInfo circuitInfo, AppearanceInfo appearance, TeleportFlags flags, out string seedCapsURI)
        {
            GridUserInfo gu;
            RegionInfo ri;
            switch(destinationInfo.StartLocation)
            {
                case "home":
                    if(m_GridUserService.TryGetValue(account.Principal.ID, out gu) &&
                        m_GridService.TryGetValue(gu.HomeRegionID, out ri))
                    {
                        destinationInfo.UpdateFromRegion(ri);
                        destinationInfo.Position = gu.HomePosition;
                        destinationInfo.LookAt = gu.HomeLookAt;
                        destinationInfo.TeleportFlags = flags | TeleportFlags.ViaHome;
                    }
                    break;

                case "last":
                    if (m_GridUserService.TryGetValue(account.Principal.ID, out gu) &&
                        m_GridService.TryGetValue(gu.LastRegionID, out ri))
                    {
                        destinationInfo.UpdateFromRegion(ri);
                        destinationInfo.Position = gu.HomePosition;
                        destinationInfo.LookAt = gu.HomeLookAt;
                        destinationInfo.TeleportFlags = flags | TeleportFlags.ViaLocation;
                    }
                    break;

                default:
                    Regex uriRegex = new Regex(@"^uri:([^&]+)&(\d+)&(\d+)&(\d+)$");
                    Match uriMatch = uriRegex.Match(destinationInfo.StartLocation);
                    if(!uriMatch.Success)
                    {
                        throw new OpenSimTeleportProtocol.TeleportFailedException("Invalid URI");
                    }
                    else
                    {
                        string regionName = uriMatch.Groups[1].Value;
                        if(regionName.Contains('@'))
                        {
                            /* HG URL */
                            throw new OpenSimTeleportProtocol.TeleportFailedException("HG URI not implemented");
                        }
                        else if (m_GridService.TryGetValue(account.ScopeID, uriMatch.Groups[1].Value, out ri))
                        {
                            destinationInfo.Position = new Vector3(
                                double.Parse(uriMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                                double.Parse(uriMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
                                double.Parse(uriMatch.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture));
                            destinationInfo.StartLocation = "url";
                        }
                    }
                    break;
            }

            OpenSimTeleportProtocol.ProtocolVersion protoVersion;
            string lastMessage = string.Empty;
            if(destinationInfo.ID != UUID.Zero)
            {
                /* try specified destination first */
                destinationInfo.TeleportFlags = flags | (destinationInfo.LocalToGrid ? TeleportFlags.ViaLogin : TeleportFlags.ViaHGLogin);
                try
                {
                    protoVersion = QueryAccess(destinationInfo, account, destinationInfo.Position);

                    int maxWearables = (int)WearableType.NumWearables;
                    if (protoVersion.Major == 0 && protoVersion.Minor < 4)
                    {
                        maxWearables = 15;
                    }
                    PostAgent(account, clientInfo, sessionInfo, destinationInfo, circuitInfo, appearance, UUID.Random, maxWearables, out seedCapsURI);
                    return;
                }
                catch(Exception e)
                {
                    m_Log.Debug(string.Format("Failed to login {0} {1} to original destination {2} ({3})", account.Principal.FirstName, account.Principal.LastName, destinationInfo.Name, destinationInfo.ID), e);
                    lastMessage = e.Message;
                }
            }

            List<RegionInfo> fallbackRegions = m_GridService.GetFallbackRegions(account.ScopeID);
            
            foreach(RegionInfo fallbackRegion in fallbackRegions)
            {
                destinationInfo.UpdateFromRegion(fallbackRegion);
                destinationInfo.StartLocation = "safe";
                destinationInfo.Position = new Vector3(128, 128, 23);
                destinationInfo.LookAt = Vector3.UnitX;
                destinationInfo.TeleportFlags = flags | TeleportFlags.ViaRegionID;

                try
                {
                    protoVersion = QueryAccess(destinationInfo, account, destinationInfo.Position);
                    int maxWearables = (int)WearableType.NumWearables;
                    if (protoVersion.Major == 0 && protoVersion.Minor < 4)
                    {
                        maxWearables = 15;
                    }
                    PostAgent(account, clientInfo, sessionInfo, destinationInfo, circuitInfo, appearance, UUID.Random, maxWearables, out seedCapsURI);
                    return;
                }
                catch (Exception e)
                {
                    m_Log.Debug(string.Format("Failed to login {0} {1} to fallback destination {2} ({3})", account.Principal.FirstName, account.Principal.LastName, destinationInfo.Name, destinationInfo.ID), e);
                    if (string.IsNullOrEmpty(lastMessage))
                    {
                        lastMessage = e.Message;
                    }
                }
            }
            throw new OpenSimTeleportProtocol.TeleportFailedException("No suitable destination found");
        }

        OpenSimTeleportProtocol.ProtocolVersion QueryAccess(DestinationInfo dInfo, UserAccount account, Vector3 position)
        {
            string uri = OpenSimTeleportProtocol.BuildAgentUri(dInfo, account.Principal.ID);

            Map req = new Map();
            req.Add("position", position.ToString());
            req.Add("my_version", "SIMULATION/0.3");
            if (null != account.Principal.HomeURI)
            {
                req.Add("agent_home_uri", account.Principal.HomeURI);
            }

            Map jsonres;
            using (Stream s = HttpRequestHandler.DoStreamRequest("QUERYACCESS", uri, null, "application/json", Json.Serialize(req), false, TimeoutMs))
            {
                jsonres = Json.Deserialize(s) as Map;
            }

            if (jsonres == null)
            {
                throw new OpenSimTeleportProtocol.TeleportFailedException("Teleport Protocol Error");
            }
            if (!jsonres["success"].AsBoolean)
            {
                throw new OpenSimTeleportProtocol.TeleportFailedException(jsonres["reason"].ToString());
            }

            string version = jsonres["version"].ToString();
            if (!version.StartsWith("SIMULATION/"))
            {
                throw new OpenSimTeleportProtocol.TeleportFailedException("Teleport Protocol Error");
            }

            OpenSimTeleportProtocol.ProtocolVersion protoVersion = new OpenSimTeleportProtocol.ProtocolVersion(version);

            if (protoVersion.Major > 0)
            {
                protoVersion.Major = 0;
                protoVersion.Minor = 3;
            }
            else if (protoVersion.Major == 0 && protoVersion.Minor > 3)
            {
                protoVersion.Minor = 3;
            }
            return protoVersion;
        }

        void PostAgent_Local(UserAccount account, ClientInfo clientInfo, SessionInfo sessionInfo, DestinationInfo destinationInfo, CircuitInfo circuitInfo, AppearanceInfo appearance, UUID capsId, int maxAllowedWearables, out string capsPath)
        {
            UserAgentServiceInterface userAgentService;

            SceneInterface scene;
            if (!m_Scenes.TryGetValue(destinationInfo.ID, out scene))
            {
                throw new OpenSimTeleportProtocol.TeleportFailedException(string.Format("No destination for agent {0}", account.Principal.FullName));
            }

            /* We have established trust of home grid by verifying its agent. 
             * At least agent and grid belong together.
             * 
             * Now, we can validate the access of the agent.
             */
            AuthorizationServiceInterface.AuthorizationData ad = new AuthorizationServiceInterface.AuthorizationData();
            ad.ClientInfo = clientInfo;
            ad.SessionInfo = sessionInfo;
            ad.AccountInfo = account;
            ad.DestinationInfo = destinationInfo;
            ad.AppearanceInfo = appearance;

            foreach (AuthorizationServiceInterface authService in m_AuthorizationServices)
            {
                authService.Authorize(ad);
            }

            try
            {
                IAgent sceneAgent = scene.Agents[account.Principal.ID];
                if (sceneAgent.Owner.EqualsGrid(account.Principal))
                {
                    if (circuitInfo.IsChild && !sceneAgent.IsInScene(scene))
                    {
                        /* already got an agent here */
                        m_Log.WarnFormat("Failed to create agent due to duplicate agent id. {0} != {1}", sceneAgent.Owner.ToString(), account.Principal.ToString());
                        throw new OpenSimTeleportProtocol.TeleportFailedException("Failed to create agent due to duplicate agent id");
                    }
                    else if (!circuitInfo.IsChild && !sceneAgent.IsInScene(scene))
                    {
                        /* child becomes root */
                        throw new OpenSimTeleportProtocol.TeleportFailedException("Teleport destination not yet implemented");
                    }
                }
                else if (sceneAgent.Owner.ID == account.Principal.ID)
                {
                    /* we got an agent already and no grid match? */
                    m_Log.WarnFormat("Failed to create agent due to duplicate agent id. {0} != {1}", sceneAgent.Owner.ToString(), account.Principal.ToString());
                    throw new OpenSimTeleportProtocol.TeleportFailedException("Failed to create agent due to duplicate agent id");
                }
            }
            catch
            {
                /* no action needed */
            }

            userAgentService = new RobustUserAgentConnector(account.Principal.HomeURI.ToString());

            GridServiceInterface gridService = scene.GridService;

            AgentServiceList serviceList = new AgentServiceList();
            serviceList.Add(m_LocalAssetService);
            serviceList.Add(m_LocalInventoryService);
            if (null != m_LocalGroupsService)
            {
                serviceList.Add(m_LocalGroupsService);
            }
            if (null != m_LocalProfileService)
            {
                serviceList.Add(m_LocalProfileService);
            }
            serviceList.Add(m_LocalFriendsService);
            serviceList.Add(userAgentService);
            serviceList.Add(m_LocalPresenceService);
            serviceList.Add(m_GridUserService);
            serviceList.Add(gridService);
            serviceList.Add(m_LocalOfflineIMService);
            serviceList.Add(new OpenSimTeleportProtocol(
                m_Commands,
                m_CapsRedirector,
                m_PacketHandlerPlugins,
                m_Scenes));

            ViewerAgent agent = new ViewerAgent(
                m_Scenes,
                account.Principal.ID,
                account.Principal.FirstName,
                account.Principal.LastName,
                account.Principal.HomeURI,
                sessionInfo.SessionID,
                sessionInfo.SecureSessionID,
                sessionInfo.ServiceSessionID,
                clientInfo,
                account,
                serviceList);
            agent.ServiceURLs = account.ServiceURLs;

            agent.Appearance = appearance;
            try
            {
                scene.DetermineInitialAgentLocation(agent, destinationInfo.TeleportFlags, destinationInfo.Location, destinationInfo.LookAt);
            }
            catch (Exception e)
            {
                m_Log.InfoFormat("Failed to determine initial location for agent {0}: {1}: {2}", account.Principal.FullName, e.GetType().FullName, e.Message);
                throw new OpenSimTeleportProtocol.TeleportFailedException(e.Message);
            }

            UDPCircuitsManager udpServer = (UDPCircuitsManager)scene.UDPServer;

            IPAddress ipAddr;
            if (!IPAddress.TryParse(clientInfo.ClientIP, out ipAddr))
            {
                m_Log.InfoFormat("Invalid IP address for agent {0}", account.Principal.FullName);
                throw new OpenSimTeleportProtocol.TeleportFailedException("Invalid IP address");
            }
            IPEndPoint ep = new IPEndPoint(ipAddr, 0);
            AgentCircuit circuit = new AgentCircuit(
                m_Commands,
                agent,
                udpServer,
                circuitInfo.CircuitCode,
                m_CapsRedirector,
                circuitInfo.CapsPath,
                agent.ServiceURLs,
                m_GatekeeperURI,
                m_PacketHandlerPlugins,
                ep);
            circuit.LastTeleportFlags = destinationInfo.TeleportFlags;
            circuit.Agent = agent;
            circuit.AgentID = account.Principal.ID;
            circuit.SessionID = sessionInfo.SessionID;
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
                throw new OpenSimTeleportProtocol.TeleportFailedException(e.Message);
            }
            if (!circuitInfo.IsChild)
            {
                /* make agent a root agent */
                agent.SceneID = scene.ID;
                if (null != m_GridUserService)
                {
                    try
                    {
                        m_GridUserService.SetPosition(agent.Owner, scene.ID, agent.GlobalPosition, agent.LookAt);
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
                pinfo.SecureSessionID = sessionInfo.SecureSessionID;
                pinfo.RegionID = scene.ID;
                m_LocalPresenceService[agent.SessionID, agent.ID, PresenceServiceInterface.SetType.Report] = pinfo;
            }
            catch (Exception e)
            {
                m_Log.Warn("Could not contact PresenceService", e);
            }
            circuit.LogIncomingAgent(m_Log, circuitInfo.IsChild);
            capsPath = OpenSimTeleportProtocol.NewCapsURL(destinationInfo.ServerURI, circuitInfo.CapsPath);
        }

        void PostAgent(UserAccount account, ClientInfo clientInfo, SessionInfo sessionInfo, DestinationInfo destinationInfo, CircuitInfo circuitInfo, AppearanceInfo appearance, UUID capsId, int maxAllowedWearables, out string capsPath)
        {
            string uri = destinationInfo.ServerURI;
            if (!uri.EndsWith("/"))
            {
                uri += "/";
            }
            if(uri == m_HttpServer.ServerURI)
            {
                PostAgent_Local(account, clientInfo, sessionInfo, destinationInfo, circuitInfo, appearance, capsId, maxAllowedWearables, out capsPath);
                return;
            }

            PostData agentPostData = new PostData();

            agentPostData.Account = account;

            agentPostData.Appearance = appearance;

            agentPostData.Circuit = circuitInfo;
            agentPostData.Circuit.CircuitCode = OpenSimTeleportProtocol.NewCircuitCode;
            agentPostData.Circuit.CapsPath = capsId.ToString();
            agentPostData.Circuit.IsChild = false;

            agentPostData.Client = clientInfo;

            agentPostData.Destination = destinationInfo;

            agentPostData.Session = sessionInfo;

            string agentURL = OpenSimTeleportProtocol.BuildAgentUri(destinationInfo, account.Principal.ID);

            byte[] uncompressed_postdata;
            using (MemoryStream ms = new MemoryStream())
            {
                agentPostData.Serialize(ms, maxAllowedWearables);
                uncompressed_postdata = ms.ToArray();
            }

            Map result;
            byte[] compressed_postdata;
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress))
                {
                    gz.Write(uncompressed_postdata, 0, uncompressed_postdata.Length);
                    compressed_postdata = ms.ToArray();
                }
            }

            m_Log.DebugFormat("Connecting to agent URL {0}", agentURL);
            try
            {
                using (Stream o = HttpRequestHandler.DoStreamRequest("POST", agentURL, null, "application/json", compressed_postdata.Length, delegate (Stream ws)
                {
                    ws.Write(compressed_postdata, 0, compressed_postdata.Length);
                }, true, TimeoutMs))
                {
                    result = (Map)Json.Deserialize(o);
                }
            }
            catch
            {
                try
                {
                    using (Stream o = HttpRequestHandler.DoStreamRequest("POST", agentURL, null, "application/x-gzip", compressed_postdata.Length, delegate (Stream ws)
                    {
                        ws.Write(compressed_postdata, 0, compressed_postdata.Length);
                    }, false, TimeoutMs))
                    {
                        result = (Map)Json.Deserialize(o);
                    }
                }
                catch
                {
                    try
                    {
                        using (Stream o = HttpRequestHandler.DoStreamRequest("POST", agentURL, null, "application/json", uncompressed_postdata.Length, delegate (Stream ws)
                        {
                            ws.Write(uncompressed_postdata, 0, uncompressed_postdata.Length);
                        }, false, TimeoutMs))
                        {
                            result = (Map)Json.Deserialize(o);
                        }
                    }
                    catch (Exception e)
                    {
                        m_Log.Debug("Connecting to agent URL " + agentURL + " failed", e);
                        /* connect failed */
                        throw new OpenSimTeleportProtocol.TeleportFailedException(e.Message);
                    }
                }
            }

            if (result.ContainsKey("success"))
            {
                if (!result["success"].AsBoolean)
                {
                    /* not authorized */
                    throw new OpenSimTeleportProtocol.TeleportFailedException("Not authorized");
                }
            }
            else if (result.ContainsKey("reason"))
            {
                if (result["reason"].ToString() != "authorized")
                {
                    /* not authorized */
                    throw new OpenSimTeleportProtocol.TeleportFailedException(result["reason"].ToString());
                }
            }
            else
            {
                /* not authorized */
                throw new OpenSimTeleportProtocol.TeleportFailedException("Not authorized");
            }

            capsPath = OpenSimTeleportProtocol.NewCapsURL(destinationInfo.ServerURI, agentPostData.Circuit.CapsPath);
        }
    }

    [PluginName("LoginProtocol")]
    public class OpenSimLoginProtocolFactory : IPluginFactory
    {
        public OpenSimLoginProtocolFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new OpenSimLoginProtocol(ownSection);
        }
    }
}
