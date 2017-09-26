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
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Authorization;
using SilverSim.Types;
using SilverSim.Types.Agent;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;

namespace SilverSim.BackendConnectors.OpenSim.PostAgent
{
    [PluginName("RemotePostAgentConnector")]
    [Description("Remote Post Agent Connector")]
    public class RemotePostAgentConnector : PostAgentConnector
    {
        protected static readonly ILog m_Log = LogManager.GetLogger("REMOTE POST AGENT CONNECTOR");

        public int TimeoutMs { get; set; }
        private readonly string GatekeeperURI;

        public RemotePostAgentConnector(IConfig ownSection)
        {
            TimeoutMs = 20000;
            GatekeeperURI = ownSection.GetString("GatekeeperURI", string.Empty);
        }

        private static string BuildAgentUri(RegionInfo destinationRegion, UUID agentID, string extra = "")
        {
            string agentURL = destinationRegion.ServerURI;

            if (!agentURL.EndsWith("/"))
            {
                agentURL += "/";
            }

            return agentURL + "agent/" + agentID.ToString() + "/" + destinationRegion.ID.ToString() + "/" + extra;
        }

        public override void PostAgent(CircuitInfo circuitInfo, AuthorizationServiceInterface.AuthorizationData authData)
        {
            var agentPostData = new PostData();

            agentPostData.Account = authData.AccountInfo;

            agentPostData.Appearance = authData.AppearanceInfo;

            agentPostData.Circuit = circuitInfo;
            agentPostData.Client = authData.ClientInfo;

            agentPostData.Destination = authData.DestinationInfo;

            agentPostData.Session = authData.SessionInfo;

            string agentURL;

            if(0 == string.Compare(authData.DestinationInfo.GatekeeperURI, GatekeeperURI, true))
            {
                agentURL = BuildAgentUri(authData.DestinationInfo, authData.AccountInfo.Principal.ID);
            }
            else
            {
                agentURL = authData.DestinationInfo.GatekeeperURI;
                if(!agentURL.EndsWith("/"))
                {
                    agentURL += "/";
                }
                agentURL += "foreignagent/" + authData.AccountInfo.Principal.ID;
            }

            byte[] uncompressed_postdata;
            using (var ms = new MemoryStream())
            {
                agentPostData.Serialize(ms, (int)WearableType.NumWearables);
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

#if DEBUG
            m_Log.DebugFormat("Sending preferred POST request to {0}: Length={1}", agentURL, compressed_postdata.Length);
#endif

            try
            {
                using (Stream o = HttpClient.DoStreamRequest("POST", agentURL, null, "application/json", compressed_postdata.Length, (Stream ws) =>
                    ws.Write(compressed_postdata, 0, compressed_postdata.Length), true, TimeoutMs))
                {
                    result = (Map)Json.Deserialize(o);
                }
            }
            catch
            {
                try
                {
                    using (Stream o = HttpClient.DoStreamRequest("POST", agentURL, null, "application/x-gzip", compressed_postdata.Length, (Stream ws) =>
                        ws.Write(compressed_postdata, 0, compressed_postdata.Length), false, TimeoutMs))
                    {
                        result = (Map)Json.Deserialize(o);
                    }
                }
                catch
                {
                    try
                    {
                        using (Stream o = HttpClient.DoStreamRequest("POST", agentURL, null, "application/json", uncompressed_postdata.Length, (Stream ws) =>
                            ws.Write(uncompressed_postdata, 0, uncompressed_postdata.Length), false, TimeoutMs))
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
                    if (result.ContainsKey("reason"))
                    {
                        /* not authorized */
                        throw new OpenSimTeleportProtocol.TeleportFailedException(result["reason"].ToString());
                    }
                    else
                    {
                        throw new OpenSimTeleportProtocol.TeleportFailedException("Not authorized");
                    }
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
        }
    }
}
