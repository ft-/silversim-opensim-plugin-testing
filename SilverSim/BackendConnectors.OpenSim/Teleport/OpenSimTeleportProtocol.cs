// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.Scene.Types.Agent;
using SilverSim.Scene.Types.Neighbor;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.Teleport;
using SilverSim.Types.StructuredData.Json;
using SilverSim.Types;
using SilverSim.Types.Agent;
using SilverSim.Types.Grid;
using System;
using System.IO;
using System.IO.Compression;
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;

namespace SilverSim.BackendConnectors.OpenSim.Teleport
{
    public class OpenSimTeleportProtocol : TeleportHandlerServiceInterface
    {
        private static Random m_RandomNumber = new Random();
        private static object m_RandomNumberLock = new object();
        public int TimeoutMs = 30000;

        private uint NewCircuitCode
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

        private string NewCapsURL(string serverURI)
        {
            return serverURI + "CAPS/" + UUID.Random.ToString() + "0000/";
        }

        public OpenSimTeleportProtocol()
        {

        }

        public override GridType GridType
        {
            get
            {
                return new GridType("opensim");
            }
        }

        public override void Cancel()
        {
        }

        public override void CloseAgentOnRelease(UUID fromSceneID)
        {
        }

        public override void ReleaseAgent(UUID fromSceneID)
        {
        }

        public override void ReleaseAgent(UUID fromSceneID, IAgent agent, RegionInfo regionInfo)
        {
            string uri = regionInfo.ServerURI + "agent/" + agent.ID.ToString() + "/" + regionInfo.ID.ToString() + "/rekease";
            HttpRequestHandler.DoRequest("DELETE", uri, null, string.Empty, string.Empty, false, TimeoutMs);
            agent.ActiveChilds.Remove(regionInfo.ID);
        }

        public override void EnableSimulator(UUID fromSceneID, IAgent agent, DestinationInfo destinationRegion)
        {
            PostData agentPostData = new PostData();

            AgentChildInfo childInfo = new AgentChildInfo();
            childInfo.DestinationInfo = destinationRegion;
            childInfo.TeleportService = this;
            agent.ActiveChilds.Add(destinationRegion.ID, childInfo);

            agentPostData.Account = agent.UntrustedAccountInfo;
            
            agentPostData.Appearance = agent.Appearance;
            
            agentPostData.Circuit = new CircuitInfo();
            agentPostData.Circuit.CircuitCode = NewCircuitCode;
            agentPostData.Circuit.CapsPath = NewCapsURL(destinationRegion.ServerURI);
            agentPostData.Circuit.IsChild = true;

            agentPostData.Client = agent.Client;
            
            agentPostData.Destination = destinationRegion;

            agentPostData.Session = agent.Session;

            string agentURL = destinationRegion.ServerURI + "agent/" + agent.ID.ToString() + "/";

            byte[] uncompressed_postdata;
            using(MemoryStream ms = new MemoryStream())
            {
                agentPostData.Serialize(ms);
                uncompressed_postdata = ms.GetBuffer();
            }

            Map result;
            try
            {
                using(Stream o = HttpRequestHandler.DoStreamRequest("POST", agentURL, null, "application/json", uncompressed_postdata.Length, delegate(Stream ws)
                {
                    ws.Write(uncompressed_postdata, 0, uncompressed_postdata.Length);
                }, true, TimeoutMs))
                {
                    result = (Map)Json.Deserialize(o);
                }
            }
            catch
            {
                try
                {
                    byte[] compressed_postdata;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress))
                        {
                            gz.Write(uncompressed_postdata, 0, uncompressed_postdata.Length);
                            compressed_postdata = ms.GetBuffer();
                        }
                    }
                    using(Stream o = HttpRequestHandler.DoStreamRequest("POST", agentURL, null, "application/x-gzip", compressed_postdata.Length, delegate(Stream ws)
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
                        using(Stream o = HttpRequestHandler.DoStreamRequest("POST", agentURL, null, "application/json", uncompressed_postdata.Length, delegate(Stream ws)
                        {
                            ws.Write(uncompressed_postdata, 0, uncompressed_postdata.Length);
                        }, false, TimeoutMs))
                        {
                            result = (Map)Json.Deserialize(o);
                        }
                    }
                    catch
                    {
                        /* connect failed */
                        agent.ActiveChilds.Remove(destinationRegion.ID);
                        return;
                    }
                }
            }

            if (result.ContainsKey("success"))
            {
                if (!result["success"].AsBoolean)
                {
                    /* not authorized */
                    return;
                }
            }
            else if (result.ContainsKey("reason"))
            {
                if (result["reason"].ToString() != "authorized")
                {
                    /* not authorized */
                    return;
                }
            }
            else
            {
                /* not authorized */
                return;
            }

            /* this makes the viewer go for a login to a neighbor */
            agent.EnableSimulator(fromSceneID, agentPostData.Circuit.CircuitCode, agentPostData.Circuit.CapsPath, destinationRegion);
        }

        public override void DisableSimulator(UUID fromSceneID, IAgent agent, RegionInfo regionInfo)
        {
            string uri = regionInfo.ServerURI + "agent/" + agent.ID.ToString() + "/" + regionInfo.ID.ToString() + "/?auth=" + agent.Session.SessionID.ToString();
            HttpRequestHandler.DoRequest("DELETE", uri, null, string.Empty, string.Empty, false, TimeoutMs);
            agent.ActiveChilds.Remove(regionInfo.ID);
        }
    }
}
