// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.Scene.Types.Agent;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.Teleport;
using SilverSim.StructuredData.Agent;
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.Agent;
using SilverSim.Types.Grid;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

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
                return new GridType("opensim-robust");
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

        public override void EnableSimulator(UUID fromSceneID, IAgent agent, DestinationInfo destinationRegion)
        {
            PostData agentPostData = new PostData();
            
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
                result = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamRequest("POST", agentURL, null, "application/json", uncompressed_postdata.Length, delegate(Stream ws)
                {
                    ws.Write(uncompressed_postdata, 0, uncompressed_postdata.Length);
                }, true, TimeoutMs));
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
                    result = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamRequest("POST", agentURL, null, "application/x-gzip", compressed_postdata.Length, delegate(Stream ws)
                    {
                        ws.Write(compressed_postdata, 0, compressed_postdata.Length);
                    }, false, TimeoutMs));
                }
                catch
                {
                    try
                    {
                        result = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamRequest("POST", agentURL, null, "application/json", uncompressed_postdata.Length, delegate(Stream ws)
                        {
                            ws.Write(uncompressed_postdata, 0, uncompressed_postdata.Length);
                        }, false, TimeoutMs));
                    }
                    catch
                    {
                        /* connect failed */
                        return;
                    }
                }
            }

            /* this makes the viewer go for a login into a neighbor */
            agent.EnableSimulator(fromSceneID, agentPostData.Circuit.CircuitCode, agentPostData.Circuit.CapsPath, destinationRegion);
        }

        public override void DisableSimulator(UUID fromSceneID, IAgent agent, RegionInfo regionInfo)
        {

        }
    }
}
