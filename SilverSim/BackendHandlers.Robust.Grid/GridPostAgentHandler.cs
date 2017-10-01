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
using SilverSim.BackendConnectors.OpenSim.PostAgent;
using SilverSim.BackendConnectors.OpenSim.Teleport;
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.ServiceInterfaces.Teleport;
using SilverSim.ServiceInterfaces.Authorization;
using SilverSim.Types;
using SilverSim.Types.Agent;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace SilverSim.BackendHandlers.Robust.Grid
{
    public abstract class GridPostAgentHandler : IPlugin
    {
        /* CAUTION! Never ever make a protocol version configurable */
        private const int PROTOCOL_VERSION_MAJOR = 0;
        private const int PROTOCOL_VERSION_MINOR = 0;

        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRID AGENT HANDLER");
        private BaseHttpServer m_HttpServer;
        private List<AuthorizationServiceInterface> m_AuthorizationServices;
        private PostAgentConnector m_PostAgentConnector;
        protected List<AuthorizationServiceInterface> AuthorizationServices => m_AuthorizationServices;
        private readonly string m_PostAgentConnectorName;

        private readonly string m_AgentBaseURL = "/agent/";

        private string m_GatekeeperURI;

        public int TimeoutMs { get; set; }

        protected GridPostAgentHandler(string agentBaseURL, IConfig ownSection)
        {
            TimeoutMs = 20000;
            m_AgentBaseURL = agentBaseURL;
            m_PostAgentConnectorName = ownSection.GetString("PostAgentConnector", "PostAgentConnector");
            m_GatekeeperURI = ownSection.GetString("GatekeeperURI", string.Empty);
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

        public virtual void Startup(ConfigurationLoader loader)
        {
            m_Log.Info("Initializing agent post handler for " + m_AgentBaseURL);
            m_PostAgentConnector = loader.GetService<PostAgentConnector>(m_PostAgentConnectorName);
            m_AuthorizationServices = loader.GetServicesByValue<AuthorizationServiceInterface>();
            m_HttpServer = loader.HttpServer;
            if (string.IsNullOrEmpty(m_GatekeeperURI))
            {
                m_GatekeeperURI = m_HttpServer.ServerURI;
            }
            if (!m_GatekeeperURI.EndsWith("/"))
            {
                m_GatekeeperURI += "/";
            }
            m_HttpServer.StartsWithUriHandlers.Add(m_AgentBaseURL, AgentPostHandler);
            try
            {
                loader.HttpsServer.StartsWithUriHandlers.Add(m_AgentBaseURL, AgentPostHandler);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        public ShutdownOrder ShutdownOrder => ShutdownOrder.Any;

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
            if (parts.Length < 2)
            {
                throw new InvalidDataException();
            }
            else
            {
                if (!UUID.TryParse(parts[1], out agentID))
                {
                    throw new InvalidDataException();
                }
                if (parts.Length > 2 &&
                    !UUID.TryParse(parts[2], out regionID))
                {
                    throw new InvalidDataException();
                }
                if (parts.Length > 3)
                {
                    action = parts[3];
                }
            }
        }

        public void DoAgentResponse(HttpRequest req, string reason, bool success)
        {
            var resmap = new Map
            {
                { "reason", reason },
                { "success", success },
                { "your_ip", req.CallerIP }
            };
            using (HttpResponse res = req.BeginResponse())
            {
                res.ContentType = "application/json";
                using (Stream o = res.GetOutputStream())
                {
                    Json.Serialize(resmap, o);
                }
            }
        }

        private void AgentPostHandler(HttpRequest req)
        {
            if (req.Method == "POST")
            {
                AgentPostHandler_POST(req);
            }
            else
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed);
            }
        }

        private void AgentPostHandler_POST(HttpRequest req)
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
                    using (Stream gzHttpBody = new GZipStream(httpBody, CompressionMode.Decompress))
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
                    req.ErrorResponse(HttpStatusCode.UnsupportedMediaType);
                    return;
                }
            }
        }

        private void AgentPostHandler_POST(HttpRequest req, Stream httpBody, UUID agentID, UUID regionID, string action)
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

            string assetServerURI = agentPost.Account.ServiceURLs["AssetServerURI"];
            string inventoryServerURI = agentPost.Account.ServiceURLs["InventoryServerURI"];
            string gatekeeperURI = m_GatekeeperURI;

            if(!TryVerifyIdentity(req, agentPost))
            {
                return;
            }

            /* We have established trust of home grid by verifying its agent. 
             * At least agent and grid belong together.
             * 
             * Now, we can validate the access of the agent.
             */
            var ad = new AuthorizationServiceInterface.AuthorizationData()
            {
                ClientInfo = agentPost.Client,
                SessionInfo = agentPost.Session,
                AccountInfo = agentPost.Account,
                DestinationInfo = agentPost.Destination,
                AppearanceInfo = agentPost.Appearance
            };
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

            string oldgridname;
            try
            {
                oldgridname = SetTravelingData(ref ad);
            }
            catch(Exception e)
            {
                m_Log.DebugFormat("Remote response: {0}: {1}", e.GetType().FullName, e.Message);
                DoAgentResponse(req, e.Message, false);
                return;
            }

            try
            {
                m_PostAgentConnector.PostAgent(agentPost.Circuit, ad);
            }
            catch (Exception e)
            {
                m_Log.DebugFormat("Remote response: {0}: {1}", e.GetType().FullName, e.Message);
                try
                {
                    AbortTravelingData(ad, oldgridname);
                }
                catch
                {
                    /* ignore */
                }
                DoAgentResponse(req, e.Message, false);
                return;
            }
            DoAgentResponse(req, "authorized", true);
        }

        public abstract bool TryVerifyIdentity(HttpRequest req, PostData data);

        public virtual string SetTravelingData(ref AuthorizationServiceInterface.AuthorizationData ad)
        {
            return string.Empty;
        }

        public virtual void AbortTravelingData(AuthorizationServiceInterface.AuthorizationData ad, string oldgridexternalname)
        {
        }
    }
}
