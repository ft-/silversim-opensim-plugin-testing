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
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
using SilverSim.BackendConnectors.Robust.UserAgent;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Agent;
using SilverSim.Scene.Types.Scene;
using SilverSim.ServiceInterfaces.Authorization;
using SilverSim.ServiceInterfaces.ServerParam;
using SilverSim.ServiceInterfaces.UserAgents;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Agent;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Groups;
using SilverSim.Types.StructuredData.Json;
using SilverSim.Viewer.Messages.Agent;
using SilverSim.Viewer.Messages.Circuit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace SilverSim.BackendHandlers.Robust.Simulation
{
    [Description("OpenSim PostAgent Handler")]
    [PluginName("RobustAgentHandler")]
    [ServerParam("OpenSimProtocolCompatibility", ParameterType = typeof(bool), DefaultValue = true)]
    public class PostAgentHandler : IPlugin, IPluginShutdown, IServerParamListener
    {
        /* CAUTION! Never ever make a protocol version configurable */
        private const int PROTOCOL_VERSION_MAJOR = 0;
        private const int PROTOCOL_VERSION_MINOR = 6;

        protected static readonly ILog m_Log = LogManager.GetLogger("ROBUST AGENT HANDLER");
        private BaseHttpServer m_HttpServer;
        private List<AuthorizationServiceInterface> m_AuthorizationServices;
        protected SceneList Scenes { get; private set; }
        protected PostAgentConnector PostAgentConnector { get; private set; }
        private readonly string m_PostAgentConnectorName = string.Empty;

        protected List<AuthorizationServiceInterface> AuthorizationServices => m_AuthorizationServices;

        private readonly string m_AgentBaseURL = "/agent/";

        private readonly RwLockedDictionary<UUID, bool> m_OpenSimProtocolCompatibilityParams = new RwLockedDictionary<UUID, bool>();

        [ServerParam("OpenSimProtocolCompatibility")]
        public void OpenSimProtocolCompatibilityUpdated(UUID regionId, string value)
        {
            bool boolval;
            if(string.IsNullOrEmpty(value))
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

        private bool GetOpenSimProtocolCompatibility(UUID regionId)
        {
            bool boolval;
            return !(m_OpenSimProtocolCompatibilityParams.TryGetValue(regionId, out boolval) ||
                m_OpenSimProtocolCompatibilityParams.TryGetValue(UUID.Zero, out boolval)) ||
                boolval;
        }

        public PostAgentHandler(IConfig ownSection)
        {
            m_PostAgentConnectorName = ownSection.GetString("PostAgentConnector", "PostAgentConnector");
        }

        protected PostAgentHandler(string agentBaseURL, IConfig ownSection)
        {
            m_PostAgentConnectorName = ownSection.GetString("PostAgentConnector", "PostAgentConnector");
            m_AgentBaseURL = agentBaseURL;
        }
        
        public virtual void Startup(ConfigurationLoader loader)
        {
            Scenes = loader.Scenes;
            m_Log.Info("Initializing agent post handler for " + m_AgentBaseURL);
            m_AuthorizationServices = loader.GetServicesByValue<AuthorizationServiceInterface>();
            m_HttpServer = loader.HttpServer;
            PostAgentConnector = loader.GetService<PostAgentConnector>(m_PostAgentConnectorName);
            m_HttpServer.StartsWithUriHandlers.Add(m_AgentBaseURL, AgentPostHandler);
            BaseHttpServer https;
            if(loader.TryGetHttpsServer(out https))
            {
                loader.HttpsServer.StartsWithUriHandlers.Add(m_AgentBaseURL, AgentPostHandler);
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

        protected virtual void CheckScenePerms(UUID sceneID)
        {
        }

        private void AgentPostHandler(HttpRequest req)
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
                    using(Stream gzHttpBody = new GZipStream(httpBody, CompressionMode.Decompress))
                    {
                        AgentPostHandler_POST(req, gzHttpBody);
                    }
                }
                else if (req.ContentType == "application/json")
                {
                    AgentPostHandler_POST(req, httpBody);
                }
                else
                {
                    m_Log.InfoFormat("Invalid content for agent message {0}: {1}", req.RawUrl, req.ContentType);
                    req.ErrorResponse(HttpStatusCode.UnsupportedMediaType);
                    return;
                }
            }
        }

        private void AgentPostHandler_POST(HttpRequest req, Stream httpBody)
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
            if (!Scenes.TryGetValue(agentPost.Destination.ID, out scene))
            {
                m_Log.InfoFormat("No destination for agent {0}", req.RawUrl);
                req.ErrorResponse(HttpStatusCode.NotFound);
                return;
            }

            try
            {
                CheckScenePerms(agentPost.Destination.ID);
            }
            catch
            {
                m_Log.InfoFormat("No destination for agent {0}", req.RawUrl);
                req.ErrorResponse(HttpStatusCode.NotFound);
                return;
            }

            UserAgentServiceInterface userAgentService = new RobustUserAgentConnector(agentPost.Account.Principal.HomeURI.ToString());

            if (!string.IsNullOrEmpty(agentPost.Session.ServiceSessionID) || !GetOpenSimProtocolCompatibility(agentPost.Destination.ID))
            {
                try
                {
                    userAgentService.VerifyAgent(agentPost.Session.SessionID, agentPost.Session.ServiceSessionID);
                }
                catch(Exception e)
                {
                    m_Log.InfoFormat("Failed to verify agent {0} at Home Grid (Code 1): {1}", agentPost.Account.Principal.FullName, e.Message);
                    DoAgentResponse(req, "Failed to verify agent at Home Grid (Code 1)", false);
                    return;
                }
            }
            else
            {
                m_Log.InfoFormat("OpenSim protocol in use for agent {0}.", agentPost.Account.Principal.FullName);
            }

            try
            {
                userAgentService.VerifyClient(agentPost.Session.SessionID, agentPost.Client.ClientIP);
            }
            catch(Exception e)
            {
                m_Log.InfoFormat("Failed to verify client {0} at Home Grid (Code 2): {1}", agentPost.Account.Principal.FullName, e.Message);
                DoAgentResponse(req, "Failed to verify client at Home Grid (Code 2)", false);
                return;
            }

            /* We have established trust of home grid by verifying its agent. 
             * At least agent and grid belong together.
             * 
             * Now, we can validate the access of the agent.
             */
            var ad = new AuthorizationServiceInterface.AuthorizationData
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

            try
            {
                PostAgentConnector.PostAgent(agentPost.Circuit, ad);
            }
            catch (Exception e)
            {
                DoAgentResponse(req, e.Message, false);
                return;
            }
            DoAgentResponse(req, "authorized", true);
        }

        private void AgentPostHandler_PUT(HttpRequest req)
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
                    req.ErrorResponse(HttpStatusCode.UnsupportedMediaType);
                    return;
                }
            }
        }

        private void AgentPostHandler_PUT_Inner(HttpRequest req, Stream httpBody, UUID agentID, UUID regionID, string action)
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

            var param = (Map)json;
            string msgType = param.ContainsKey("message_type") ? param["message_type"].ToString() : "AgentData";
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

        private void AgentPostHandler_PUT_AgentData(HttpRequest req, UUID agentID, UUID regionID, string action, Map param)
        {
            var childAgentData = new ChildAgentUpdate();

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
                var groups = param["groups"] as AnArray;
                if (groups != null)
                {
                    foreach (IValue gval in groups)
                    {
                        var group = (Map)gval;
                        var g = new ChildAgentUpdate.GroupDataEntry
                        {
                            AcceptNotices = group["accept_notices"].AsBoolean
                        };
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
                var anims = param["animations"] as AnArray;
                if (anims != null)
                {
                    foreach (IValue aval in anims)
                    {
                        var anim = (Map)aval;
                        var a = new ChildAgentUpdate.AnimationDataEntry
                        {
                            Animation = anim["animation"].AsUUID
                        };
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
            var appearancePack = (Map)param["packed_appearance"];
            var Appearance = new AppearanceInfo
            {
                AvatarHeight = appearancePack["height"].AsReal
            };
            if (appearancePack.ContainsKey("visualparams"))
            {
                var vParams = (AnArray)appearancePack["visualparams"];
                var visualParams = new byte[vParams.Count];

                int i;
                for (i = 0; i < vParams.Count; ++i)
                {
                    visualParams[i] = (byte)vParams[i].AsUInt;
                }
                Appearance.VisualParams = visualParams;
            }

            {
                var texArray = (AnArray)appearancePack["textures"];
                int i;
                for (i = 0; i < AppearanceInfo.AvatarTextureData.TextureCount; ++i)
                {
                    Appearance.AvatarTextures[i] = texArray[i].AsUUID;
                }
            }

            {
                int i;
                uint n;
                var wearables = (AnArray)appearancePack["wearables"];
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
                        var wp = (Map)val;
                        var wi = new AgentWearables.WearableInfo
                        {
                            ItemID = wp["item"].AsUUID,
                            AssetID = wp.ContainsKey("asset") ? wp["asset"].AsUUID : UUID.Zero
                        };
                        var type = (WearableType)i;
                        Appearance.Wearables[type, n++] = wi;
                    }
                }
            }

            {
                foreach (IValue apv in (AnArray)appearancePack["attachments"])
                {
                    var ap = (Map)apv;
                    uint apid;
                    if (uint.TryParse(ap["point"].ToString(), out apid))
                    {
                        Appearance.Attachments[(AttachmentPoint)apid][ap["item"].AsUUID] = UUID.Zero;
                    }
                }
            }

            if (appearancePack.ContainsKey("serial"))
            {
                Appearance.Serial = appearancePack["serial"].AsInt;
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
            if (Scenes.TryGetValue(destinationRegionID, out scene))
            {
                IAgent agent;

                try
                {
                    agent = scene.Agents[childAgentData.AgentID];
                }
                catch
                {
                    using (HttpResponse res = req.BeginResponse())
                    using (StreamWriter s = res.GetOutputStream().UTF8StreamWriter())
                    {
                        s.Write(false.ToString());
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
                    using (Stream s = res.GetOutputStream(resultBytes.Length))
                    {
                        s.Write(resultBytes, 0, resultBytes.Length);
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

        private void AgentPostHandler_PUT_WaitForRoot_HttpResponse(object o, bool success)
        {
#if DEBUG
            m_Log.DebugFormat("respond to WaitForRoot PUT agent with {0}", success.ToString());
#endif
            var req = (HttpRequest)o;
            try
            {
                string resultStr = success.ToString();
                byte[] resultBytes = resultStr.ToUTF8Bytes();
                using (HttpResponse res = req.BeginResponse("text/plain"))
                using (Stream s = res.GetOutputStream(resultBytes.Length))
                {
                    s.Write(resultBytes, 0, resultBytes.Length);
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

        private void AgentPostHandler_PUT_WaitForRoot_CallbackURI(object o, bool success)
        {
            if (success)
            {
                try
                {
                    new HttpClient.Delete((string)o) { TimeoutMs = 10000 }.ExecuteRequest();
                }
                catch(Exception e)
                {
                    /* do not pass the exceptions */
                    m_Log.WarnFormat("Exception encountered when calling CallbackURI: {0}: {1}", e.GetType().FullName, e.Message);
                }
            }
        }

        private void AgentPostHandler_PUT_AgentPosition(HttpRequest req, UUID agentID, UUID regionID, string action, Map param)
        {
            var childAgentPosition = new ChildAgentPositionUpdate();
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
            if (Scenes.TryGetValue(destinationRegionID, out scene))
            {
                IAgent agent;
                if (!scene.Agents.TryGetValue(childAgentPosition.AgentID, out agent))
                {
                    using (HttpResponse res = req.BeginResponse())
                    using (StreamWriter s = res.GetOutputStream().UTF8StreamWriter())
                    {
                        s.Write(false.ToString());
                    }
                    return;
                }

                try
                {
                    agent.HandleMessage(childAgentPosition);
                    using (HttpResponse res = req.BeginResponse())
                    using (StreamWriter s = res.GetOutputStream().UTF8StreamWriter())
                    {
                        s.Write(true.ToString());
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

        private void AgentPostHandler_DELETE(HttpRequest req)
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
                scene = Scenes[regionID];
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.NotFound);
                return;
            }

            IAgent agent;
            try
            {
                agent = scene.Agents[agentID];
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.NotFound);
                return;
            }

            if (action == "release")
            {
                /* map this to the teleport protocol */
                /* it will make the agent become a child */
                IAgentTeleportServiceInterface teleportService = agent.ActiveTeleportService;
                if (teleportService != null)
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
                if (teleportService != null)
                {
                    /* we give the teleport handler the chance to do everything required */
                    teleportService.CloseAgentOnRelease(scene.ID);
                }
                else
                {
                    req.ErrorResponse(HttpStatusCode.Forbidden);
                    return;
                }
            }
            else
            {
                /* let the disconnect be handled by Circuit */
                agent.SendMessageAlways(new DisableSimulator(), scene.ID);
            }
            using (req.BeginResponse(HttpStatusCode.OK, "OK"))
            {
                /* all that is needed is already done */
            }
        }

        private void AgentPostHandler_QUERYACCESS(HttpRequest req)
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
                scene = Scenes[regionID];
            }
            catch
            {
                req.ErrorResponse(HttpStatusCode.NotFound);
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

            if (jsonreq == null)
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

            var response = new Map();
            bool success = true;
            string reason = string.Empty;
            int versionMajor;
            int versionMinor;

            string versionAsDouble;
            if (jsonreq.TryGetValue("simulation_service_supported_max", out versionAsDouble))
            {
                string[] myVersionSplit = versionAsDouble.Split(new char[] { '.' });
                if (myVersionSplit.Length < 2)
                {
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    return;
                }
                if (!int.TryParse(myVersionSplit[0], out versionMajor))
                {
                    versionMajor = 0;
                }
                if (!int.TryParse(myVersionSplit[1], out versionMinor))
                {
                    versionMinor = 0;
                }
            }
            else
            {
                string[] myVersionSplit = myVersion.Split(new char[] { '.', '/' });
                if (myVersionSplit.Length < 3)
                {
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    return;
                }
                if (myVersionSplit[0] != "SIMULATION")
                {
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    return;
                }
                if (!int.TryParse(myVersionSplit[1], out versionMajor))
                {
                    versionMajor = 0;
                }
                if (!int.TryParse(myVersionSplit[2], out versionMinor))
                {
                    versionMinor = 0;
                }
            }

            /* check version and limit it down to what we actually understand
             * weird but the truth of OpenSim protocol versioning
             */
            if (versionMajor > PROTOCOL_VERSION_MAJOR)
            {
                versionMajor = PROTOCOL_VERSION_MAJOR;
                versionMinor = PROTOCOL_VERSION_MINOR;
            }
            else if (PROTOCOL_VERSION_MAJOR == versionMajor && versionMinor > PROTOCOL_VERSION_MINOR)
            {
                versionMinor = PROTOCOL_VERSION_MINOR;
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

            var agentUUI = new UUI()
            {
                ID = agentID
            };
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
            string versionStr = string.Format("{0}.{1}", versionMajor, versionMinor);
            double maxVersion = double.Parse(versionStr, System.Globalization.CultureInfo.InvariantCulture);
            response.Add("version", "SIMULATION/" + versionStr);
            response.Add("negotiated_outbound_version", maxVersion);
            string acceptedMaxVersion;
            if(jsonreq.TryGetValue("simulation_service_accepted_max", out acceptedMaxVersion))
            {
                string[] myVersionSplit = acceptedMaxVersion.Split(new char[] { '.' });
                if (myVersionSplit.Length < 2)
                {
                    req.ErrorResponse(HttpStatusCode.BadRequest);
                    return;
                }
                if (!int.TryParse(myVersionSplit[0], out versionMajor))
                {
                    versionMajor = 0;
                }
                if (!int.TryParse(myVersionSplit[1], out versionMinor))
                {
                    versionMinor = 0;
                }

                if(versionMajor > PROTOCOL_VERSION_MAJOR)
                {
                    versionMajor = PROTOCOL_VERSION_MAJOR;
                    versionMinor = PROTOCOL_VERSION_MINOR;
                }
                else if(versionMajor == PROTOCOL_VERSION_MAJOR && versionMinor > PROTOCOL_VERSION_MINOR)
                {
                    versionMinor = PROTOCOL_VERSION_MINOR;
                }
                response.Add("negotiated_inbound_version", double.Parse(string.Format("{0}.{1}", versionMajor, versionMinor), System.Globalization.CultureInfo.InvariantCulture));
            }
            response.Add("features", new AnArray());
            using (HttpResponse res = req.BeginResponse("application/json"))
            using (Stream s = res.GetOutputStream())
            {
                Json.Serialize(response, s);
            }
        }
    }
}
