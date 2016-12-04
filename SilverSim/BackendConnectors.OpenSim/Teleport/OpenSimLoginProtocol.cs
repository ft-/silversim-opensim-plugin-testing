// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using Nini.Config;
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Scene.ServiceInterfaces.Teleport;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.GridUser;
using SilverSim.ServiceInterfaces.ServerParam;
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.Agent;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Grid;
using SilverSim.Types.GridUser;
using SilverSim.Types.StructuredData.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace SilverSim.BackendConnectors.OpenSim.Teleport
{
    public class OpenSimLoginProtocol : ILoginConnectorServiceInterface, IPlugin, IServerParamListener
    {
        GridUserServiceInterface m_GridUserService;
        GridServiceInterface m_GridService;

        string m_GridUserServiceName;
        string m_GridServiceName;

        public int TimeoutMs = 30000;

        public OpenSimLoginProtocol(IConfig ownConfig)
        {
            m_GridUserServiceName = ownConfig.GetString("GridUserService", "GridUserService");
            m_GridServiceName = ownConfig.GetString("GridService", "GridService");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_GridUserService = loader.GetService<GridUserServiceInterface>(m_GridUserServiceName);
            m_GridService = loader.GetService<GridServiceInterface>(m_GridServiceName);
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
                }
                catch(Exception e)
                {
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
                }
                catch (Exception e)
                {
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

        void PostAgent(UserAccount account, ClientInfo clientInfo, SessionInfo sessionInfo, DestinationInfo destinationInfo, CircuitInfo circuitInfo, AppearanceInfo appearance, UUID capsId, int maxAllowedWearables, out string capsPath)
        {
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
}
