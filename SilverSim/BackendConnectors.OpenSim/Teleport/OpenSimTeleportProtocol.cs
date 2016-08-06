// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.Rpc;
using SilverSim.Scene.Types.Agent;
using SilverSim.Scene.Types.Neighbor;
using SilverSim.Scene.Types.Scene;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.Teleport;
using SilverSim.Types;
using SilverSim.Types.Agent;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.Json;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;

namespace SilverSim.BackendConnectors.OpenSim.Teleport
{
    [Description("OpenSim Teleport Protocol")]
    public class OpenSimTeleportProtocol : TeleportHandlerServiceInterface, IPlugin
    {
        private static Random m_RandomNumber = new Random();
        private static object m_RandomNumberLock = new object();
        public int TimeoutMs = 30000;
        private Thread m_TeleportThread;
        private object m_TeleportThreadLock = new object();

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

        private string NewCapsURL(string serverURI)
        {
            return serverURI + "CAPS/" + UUID.Random.ToString() + "0000/";
        }

        public OpenSimTeleportProtocol()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
            /* no action needed */
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
            lock(m_TeleportThreadLock)
            {
                if(null != m_TeleportThread)
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

        public virtual new bool TeleportHome(SceneInterface sceneInterface, IAgent agent)
        {
            return false;
        }

        public virtual new bool TeleportTo(SceneInterface sceneInterface, IAgent agent, string regionName, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            /* foreign grid */
            lock (m_TeleportThreadLock)
            {
                if (null == m_TeleportThread)
                {
                    m_TeleportThread = new Thread(delegate ()
                    {
                        try
                        {
                            TeleportTo_Step1_RegionNameLookup(sceneInterface, agent, regionName, position, lookAt, flags);
                        }
                        catch (TeleportFailedException e)
                        {
                            agent.SendAlertMessage(e.Message, sceneInterface.ID);
                        }
                        finally
                        {
                            agent.RemoveActiveTeleportService(this);
                        }
                    });
                    agent.ActiveTeleportService = this;
                    m_TeleportThread.Start();
                    return true;
                }
            }
            return false;
        }

        public virtual new bool TeleportTo(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, GridVector location, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            if (gatekeeperURI == sceneInterface.GatekeeperURI)
            {
                /* same grid */
                lock(m_TeleportThreadLock)
                {
                    if(null == m_TeleportThreadLock)
                    {
                        m_TeleportThread = new Thread(delegate ()
                        {
                            try
                            {
                                TeleportTo_Step1_ThisGrid(sceneInterface, agent, gatekeeperURI, location, position, lookAt, flags);
                            }
                            catch (TeleportFailedException e)
                            {
                                agent.SendAlertMessage(e.Message, sceneInterface.ID);
                            }
                            finally
                            {
                                agent.RemoveActiveTeleportService(this);
                            }
                        });
                        agent.ActiveTeleportService = this;
                        m_TeleportThread.Start();
                        return true;
                    }
                }
            }
            else
            {
                /* foreign grid */
                lock (m_TeleportThreadLock)
                {
                    if (null == m_TeleportThreadLock)
                    {
                        m_TeleportThread = new Thread(delegate ()
                        {
                            try
                            {
                                TeleportTo_Step1_ForeignGrid(sceneInterface, agent, gatekeeperURI, location, position, lookAt, flags);
                            }
                            catch (TeleportFailedException e)
                            {
                                agent.SendAlertMessage(e.Message, sceneInterface.ID);
                            }
                            finally
                            {
                                agent.RemoveActiveTeleportService(this);
                            }
                        });
                        agent.ActiveTeleportService = this;
                        m_TeleportThread.Start();
                        return true;
                    }
                }
            }
            return false;
        }

        public virtual new bool TeleportTo(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, UUID regionID, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            if (gatekeeperURI == sceneInterface.GatekeeperURI)
            {
                /* same grid */
                lock (m_TeleportThreadLock)
                {
                    if (null == m_TeleportThread)
                    {
                        m_TeleportThread = new Thread(delegate ()
                        {
                            try
                            {
                                TeleportTo_Step1_ThisGrid(sceneInterface, agent, gatekeeperURI, regionID, position, lookAt, flags);
                            }
                            catch (TeleportFailedException e)
                            {
                                agent.SendAlertMessage(e.Message, sceneInterface.ID);
                            }
                            finally
                            {
                                agent.RemoveActiveTeleportService(this);
                            }
                        });
                        agent.ActiveTeleportService = this;
                        m_TeleportThread.Start();
                        return true;
                    }
                }
            }
            else
            {
                /* foreign grid */
                lock (m_TeleportThreadLock)
                {
                    if (null == m_TeleportThread)
                    {
                        m_TeleportThread = new Thread(delegate ()
                        {
                            try
                            {
                                TeleportTo_Step1_ForeignGrid(sceneInterface, agent, gatekeeperURI, regionID, position, lookAt, flags);
                            }
                            catch (TeleportFailedException e)
                            {
                                agent.SendAlertMessage(e.Message, sceneInterface.ID);
                            }
                            finally
                            {
                                agent.RemoveActiveTeleportService(this);
                            }
                        });
                        agent.ActiveTeleportService = this;
                        m_TeleportThread.Start();
                        return true;
                    }
                }
            }
            return false;
        }

        void TeleportTo_Step1_RegionNameLookup(SceneInterface sceneInterface, IAgent agent, string regionName, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            DestinationInfo dInfo = null;
            if (regionName.StartsWith("http://") || regionName.StartsWith("https://"))
            {
                /* URI style HG location */
                int pos = regionName.IndexOf(' ');
                if(pos < 0 && !Uri.IsWellFormedUriString(regionName, UriKind.Absolute))
                {
                    throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "HgUriStyleInvalid", "HG URI-Style is invalid"));
                }
                if(pos < 0)
                {
                    dInfo = GetRegionByName(regionName.Substring(0, pos), agent, string.Empty);
                }
                else
                {
                    dInfo = GetRegionByName(regionName.Substring(0, pos), agent, regionName.Substring(pos + 1));
                }
            }
            else
            {
                if (regionName.Contains(":"))
                {
                    /* HG notation based on hostname:port:region */
                    string[] parts = regionName.Split(new char[] { ':' }, 3);
                    string gkuri;
                    if (parts.Length > 1)
                    {
                        gkuri = "http://" + parts[0] + ":" + parts[1] + "/";
                        if (Uri.IsWellFormedUriString(gkuri, UriKind.Absolute))
                        {
                            switch (parts.Length)
                            {
                                case 3:
                                    /* hostname:port:region */
                                    dInfo = GetRegionByName(gkuri, agent, parts[2]);
                                    break;

                                case 2:
                                    dInfo = GetRegionByName(gkuri, agent, string.Empty);
                                    break;

                                default:
                                    break;
                            }
                        }
                    }
                }

                if(null == dInfo)
                {
                    GridServiceInterface gridService = sceneInterface.GridService;
                    if (gridService != null)
                    {
                        try
                        {
                            RegionInfo rInfo = gridService[sceneInterface.ScopeID, regionName];
                            dInfo = new DestinationInfo(rInfo);
                            dInfo.GatekeeperURI = sceneInterface.GatekeeperURI;
                            dInfo.LocalToGrid = true;
                        }
                        catch(KeyNotFoundException)
                        {
                            throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "RegionNotFound", "Region not found."));
                        }
                    }
                    else
                    {
                        throw new TeleportFailedException(this.GetLanguageString(agent.CurrentCulture, "RegionNotFound", "Region not found."));
                    }
                }
            }

            TeleportTo_Step2(sceneInterface, agent, dInfo, position, lookAt, flags);
        }

        void TeleportTo_Step1_ThisGrid(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, UUID regionID, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            GridServiceInterface gridService = sceneInterface.GridService;
            if(null == gridService)
            {
                agent.RemoveActiveTeleportService(this);
                return;
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

            DestinationInfo dInfo = new DestinationInfo(rInfo);
            dInfo.LocalToGrid = true;
            dInfo.GatekeeperURI = gatekeeperURI;

            TeleportTo_Step2(sceneInterface, agent, dInfo, position, lookAt, flags);
        }

        void TeleportTo_Step1_ForeignGrid(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, UUID regionID, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            DestinationInfo dInfo = GetRegionById(gatekeeperURI, agent, regionID);
            TeleportTo_Step2(sceneInterface, agent, dInfo, position, lookAt, flags);
        }

        void TeleportTo_Step1_ThisGrid(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, GridVector location, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            GridServiceInterface gridService = sceneInterface.GridService;
            if (null == gridService)
            {
                agent.RemoveActiveTeleportService(this);
                return;
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

            DestinationInfo dInfo = new DestinationInfo(rInfo);
            dInfo.LocalToGrid = true;
            dInfo.GatekeeperURI = gatekeeperURI;

            TeleportTo_Step2(sceneInterface, agent, dInfo, position, lookAt, flags);
        }

        void TeleportTo_Step1_ForeignGrid(SceneInterface sceneInterface, IAgent agent, string gatekeeperURI, GridVector location, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            agent.SendAlertMessage(this.GetLanguageString(agent.CurrentCulture, "TeleportNotSupported", "Teleport via location not supported into HG"), sceneInterface.ID);
            agent.RemoveActiveTeleportService(this);
        }

        void TeleportTo_Step2(SceneInterface scene, IAgent agent, DestinationInfo dInfo, Vector3 position, Vector3 lookAt, TeleportFlags flags)
        {
            throw new TeleportFailedException("Not yet implemented");
        }

        #region Gatekeeper connector
        DestinationInfo GetRegionByName(string gatekeeperuri, IAgent agent, string name)
        {
            UUID regionId;
            Map req = new Map();
            req.Add("region_name", name);
            Map response = DoXmlRpcWithHashResponse(gatekeeperuri, "link_region", req);
            if(!response["result"].AsBoolean)
            {
                return null;
            }
            regionId = response["uuid"].AsUUID;
            return GetRegionById(gatekeeperuri, agent, regionId);
        }

        DestinationInfo GetRegionById(string gatekeeperuri, IAgent agent, UUID regionId)
        {
            Map req = new Map();
            req.Add("region_uuid", regionId);
            req.Add("agent_id", agent.ID);
            if (agent.Owner.HomeURI != null)
            {
                req.Add("agent_home_uri", agent.Owner.HomeURI.ToString());
            }
            Map response = DoXmlRpcWithHashResponse(gatekeeperuri, "get_region", req);
            if(!response["result"].AsBoolean)
            {
                string message = "The teleport destination could not be found.";
                if(response.ContainsKey("message"))
                {
                    message = response["message"].ToString();
                }
                throw new TeleportFailedException(message);
            }

            DestinationInfo dInfo = new DestinationInfo();
            dInfo.GatekeeperURI = gatekeeperuri;
            dInfo.LocalToGrid = false;
            if (response.ContainsKey("x"))
            {
                dInfo.Location.GridX = (ushort)response["x"].AsUInt;
            }
            if (response.ContainsKey("y"))
            {
                dInfo.Location.GridY = (ushort)response["y"].AsUInt;
            }
            if(response.ContainsKey("size_x"))
            {
                dInfo.Size.GridX = (ushort)response["size_x"].AsUInt;
            }
            else
            {
                dInfo.Size.GridX = 1;
            }
            if (response.ContainsKey("size_y"))
            {
                dInfo.Size.GridY = (ushort)response["size_y"].AsUInt;
            }
            else
            {
                dInfo.Size.GridY = 1;
            }
            if (response.ContainsKey("region_name"))
            {
                dInfo.Name = response["region_name"].ToString();
            }

            if(response.ContainsKey("http_port"))
            {
                dInfo.ServerHttpPort = response["http_port"].AsUInt;
            }
            if(response.ContainsKey("internal_port"))
            {
                dInfo.ServerPort = response["internal_port"].AsUInt;
            }
            if (response.ContainsKey("hostname"))
            {
                IPAddress[] address = Dns.GetHostAddresses(response["hostname"].ToString());
                if (response.ContainsKey("internal_port") && address.Length > 0)
                {
                    dInfo.SimIP = new IPEndPoint(address[0], (int)dInfo.ServerPort);
                }
            }
            if (response.ContainsKey("server_uri"))
            {
                dInfo.ServerURI = response["server_uri"].ToString();
            }
            return dInfo;
        }

        Map DoXmlRpcWithHashResponse(string gatekeeperuri, string method, Map reqparams)
        {
            XmlRpc.XmlRpcRequest req = new XmlRpc.XmlRpcRequest(method);
            req.Params.Add(reqparams);
            XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(gatekeeperuri, req, TimeoutMs);

            Map hash = (Map)res.ReturnValue;
            if (hash == null)
            {
                throw new InvalidOperationException();
            }

            return hash;
        }
        #endregion

    }
}
