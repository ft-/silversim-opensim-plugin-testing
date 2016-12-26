// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Types;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;
using System;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Scene;
using SilverSim.Scene.Types.Agent;
using SilverSim.Types.IM;
using SilverSim.Types.Inventory;
using SilverSim.Types.Asset;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Viewer.Messages.Friend;
using Nini.Config;
using SilverSim.Viewer.Messages.Inventory;
using SilverSim.Types.Friends;

namespace SilverSim.BackendHandlers.Robust.Simulation
{
    public class SimFriendsHandler : IPlugin, IPluginShutdown
    {
        BaseHttpServer m_HttpServer;
        SceneList m_Scenes;

        public SimFriendsHandler()
        {

        }

        public ShutdownOrder ShutdownOrder
        {
            get
            {
                return ShutdownOrder.Any;
            }
        }

        public void Shutdown()
        {
            m_HttpServer.StartsWithUriHandlers.Remove("/friends");
            m_HttpServer = null;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Scenes = loader.Scenes;
            m_HttpServer = loader.HttpServer;
            m_HttpServer.UriHandlers.Add("/friends", FriendsHandler);
        }

        void FriendsHandler(HttpRequest httpreq)
        {
            Dictionary<string, object> req;
            try
            {
                req = REST.ParseREST(httpreq.Body);
            }
            catch
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Bad Request");
                return;
            }


            bool result = false;

            try
            {
                if (req.ContainsKey("METHOD"))
                {
                    switch (req["METHOD"].ToString())
                    {
                        case "friendship_offered":
                            result = HandleFriendshipOffered(httpreq, req);
                            break;

                        case "friendship_approved":
                            result = HandleFriendshipApproved(httpreq, req);
                            break;

                        case "friendship_denied":
                            result = HandleFriendshipDenied(httpreq, req);
                            break;

                        case "friendship_terminated":
                            result = HandleFriendshipTerminated(httpreq, req);
                            break;

                        case "grant_rights":
                            result = HandleGrantRights(httpreq, req);
                            break;

                        case "status":
                            result = HandleFriendStatus(httpreq, req);
                            break;

                        default:
                            break;
                    }
                }
            }
            catch
            {
                httpreq.ErrorResponse(HttpStatusCode.InternalServerError, "Internal Server Error");
                return;
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (Stream s = res.GetOutputStream())
                {
                    using (XmlTextWriter w = s.UTF8XmlTextWriter())
                    {
                        w.WriteStartElement("ServerResponse");
                        w.WriteNamedValue("RESULT", result);
                        w.WriteEndElement();
                    }
                }
            }
        }

        bool HandleFriendStatus(HttpRequest httpreq, Dictionary<string, object> req)
        {
            if (!req.ContainsKey("FromID") || !req.ContainsKey("ToID") || !req.ContainsKey("Online"))
            {
                return false;
            }

            UUID fromID;
            UUID toID;
            bool online;

            if (!UUID.TryParse(req["FromID"].ToString(), out fromID))
            {
                return false;
            }

            if (!UUID.TryParse(req["ToID"].ToString(), out toID))
            {
                return false;
            }

            if (!Boolean.TryParse(req["Online"].ToString(), out online))
            {
                return false;
            }

            IAgent agent;
            SceneInterface agentScene;
            if (!TryRootAgent(toID, out agent, out agentScene))
            {
                return false;
            }

            FriendStatus fStat;
            if (agent.KnownFriends.TryGetValue(fromID, out fStat))
            {
                if (online)
                {
                    if (!fStat.IsOnline)
                    {
                        fStat.IsOnline = true;
                        OnlineNotification m = new OnlineNotification();
                        m.AgentIDs.Add(fromID);
                        agent.SendMessageAlways(m, agentScene.ID);
                    }
                }
                else
                {
                    if (fStat.IsOnline)
                    {
                        fStat.IsOnline = false;
                        OfflineNotification m = new OfflineNotification();
                        m.AgentIDs.Add(fromID);
                        agent.SendMessageAlways(m, agentScene.ID);
                    }
                }
            }

            return true;
        }

        bool HandleGrantRights(HttpRequest httpreq, Dictionary<string, object> req)
        {
            if (!req.ContainsKey("FromID") || !req.ContainsKey("ToID") || !req.ContainsKey("Rights"))
            {
                return false;
            }

            UUID fromID;
            UUID toID;
            int newRights;

            if (!UUID.TryParse(req["FromID"].ToString(), out fromID))
            {
                return false;
            }

            if (!UUID.TryParse(req["ToID"].ToString(), out toID))
            {
                return false;
            }

            if (!int.TryParse(req["Rights"].ToString(), out newRights))
            {
                return false;
            }

            IAgent agent;
            SceneInterface agentScene;
            if (!TryRootAgent(toID, out agent, out agentScene))
            {
                return false;
            }

            GridInstantMessage gim = new GridInstantMessage();
            if (!agentScene.AvatarNameService.TryGetValue(fromID, out gim.FromAgent))
            {
                return false;
            }

            FriendStatus fi;
            if(agent.KnownFriends.TryGetValue(fromID, out fi) && fi.Friend.EqualsGrid(gim.FromAgent))
            {
                fi.UserGivenFlags = (FriendRightFlags)newRights;
                ChangeUserRights m = new ChangeUserRights();
                m.AgentID = agent.Owner.ID;
                m.SessionID = agent.Session.SessionID;

                ChangeUserRights.RightsEntry r = new ChangeUserRights.RightsEntry();
                r.AgentRelated = gim.FromAgent.ID;
                r.RelatedRights = newRights;
                m.Rights.Add(r);

                agent.SendMessageAlways(m, agentScene.ID);
            }

            return true;
        }

        bool HandleFriendshipTerminated(HttpRequest httpreq, Dictionary<string, object> req)
        {
            if(!req.ContainsKey("FromID") || !req.ContainsKey("ToID"))
            {
                return false;
            }

            UUID fromID;
            UUID toID;

            if (!UUID.TryParse(req["FromID"].ToString(), out fromID))
            {
                return false;
            }

            if (!UUID.TryParse(req["ToID"].ToString(), out toID))
            {
                return false;
            }

            IAgent agent;
            SceneInterface agentScene;
            if (!TryRootAgent(toID, out agent, out agentScene))
            {
                return false;
            }

            TerminateFriendship m = new TerminateFriendship();
            m.AgentID = agent.Owner.ID;
            m.SessionID = agent.Session.SessionID;
            m.OtherID = fromID;

            agent.SendMessageAlways(m, agentScene.ID);

            agent.KnownFriends.Remove(fromID);

            return true;
        }

        bool HandleFriendshipDenied(HttpRequest httpreq, Dictionary<string, object> req)
        {
            UUID fromID = UUID.Zero;
            UUID toID = UUID.Zero;

            if(!req.ContainsKey("FromID") || !req.ContainsKey("ToID"))
            {
                return false;
            }

            if (!UUID.TryParse(req["FromID"].ToString(), out fromID))
            {
                return false;
            }

            if (!UUID.TryParse(req["ToID"].ToString(), out toID))
            {
                return false;
            }

            IAgent agent;
            SceneInterface agentScene;
            if (!TryRootAgent(toID, out agent, out agentScene))
            {
                return false;
            }

            GridInstantMessage gim = new GridInstantMessage();
            if (!agentScene.AvatarNameService.TryGetValue(fromID, out gim.FromAgent))
            {
                return false;
            }

            gim.ToAgent = agent.Owner;
            gim.Dialog = GridInstantMessageDialog.FriendshipDeclined;
            gim.Message = gim.ToAgent.ID.ToString();
            gim.RegionID = agentScene.ID;
            gim.ParentEstateID = agentScene.ParentEstateID;

            return agent.IMSend(gim);
        }

        bool HandleFriendshipApproved(HttpRequest httpreq, Dictionary<string, object> req)
        {
            UUID fromID = UUID.Zero;
            UUID toID = UUID.Zero;

            if (!req.ContainsKey("FromID") || !req.ContainsKey("ToID"))
            {
                return false;
            }

            /* there is no real sense to use FromName without a HomeURI */

            IAgent agent;
            SceneInterface agentScene;
            if (!TryRootAgent(toID, out agent, out agentScene))
            {
                return false;
            }

            GridInstantMessage gim = new GridInstantMessage();
            if (!agentScene.AvatarNameService.TryGetValue(fromID, out gim.FromAgent))
            {
                return false;
            }

            gim.ToAgent = agent.Owner;
            gim.Dialog = GridInstantMessageDialog.FriendshipAccepted;
            gim.Message = gim.ToAgent.ID.ToString();
            gim.RegionID = agentScene.ID;
            gim.ParentEstateID = agentScene.ParentEstateID;

            InventoryFolder folder;
            InventoryServiceInterface inventoryService = agent.InventoryService;
            if(inventoryService.Folder.TryGetValue(gim.ToAgent.ID, AssetType.CallingCard, out folder))
            {
                InventoryItem item = new InventoryItem();

                item.AssetID = UUID.Zero;
                item.AssetType = AssetType.CallingCard;
                item.Permissions.Base = InventoryPermissionsMask.Copy | InventoryPermissionsMask.Modify;

                item.Permissions.EveryOne = InventoryPermissionsMask.None;
                item.Permissions.Current = item.Permissions.Base;
                item.Permissions.NextOwner = InventoryPermissionsMask.Copy | InventoryPermissionsMask.Modify;

                item.ID = UUID.Random;
                item.Creator = gim.FromAgent;
                item.Owner = gim.ToAgent;
                item.Group = UGI.Unknown;
                item.IsGroupOwned = false;
                item.ParentFolderID = folder.ID;

                item.CreationDate = Date.Now;
                item.InventoryType = InventoryType.CallingCard;
                item.Flags = 0;

                item.Name = gim.ToAgent.FullName;
                item.Description = "";

                item.SaleInfo.Price = 10;
                item.SaleInfo.Type = InventoryItem.SaleInfoData.SaleType.NoSale;

                inventoryService.Item.Add(item);

                UpdateCreateInventoryItem m = new UpdateCreateInventoryItem();
                m.AgentID = toID;
                m.SimApproved = true;
                m.TransactionID = UUID.Zero;
                m.AddItem(item, 0);
                agent.SendMessageAlways(m, agentScene.ID);
            }

            if(null != agent.FriendsService)
            {
                FriendInfo fi;
                if(agent.FriendsService.TryGetValue(gim.ToAgent, gim.FromAgent, out fi))
                {
                    FriendStatus fStat = new FriendStatus(fi);
                    fStat.IsOnline = true;
                    agent.KnownFriends[fromID] = fStat;

                    if(fStat.UserGivenFlags.HasFlag(FriendRightFlags.SeeOnline))
                    {
                        OnlineNotification notification = new OnlineNotification();
                        notification.AgentIDs.Add(fromID);
                        agent.SendMessageAlways(notification, agentScene.ID);
                    }
                }
            }

            return agent.IMSend(gim);
        }

        bool HandleFriendshipOffered(HttpRequest httpreq, Dictionary<string, object> req)
        {
            UUID fromID;
            UUID toID;
            if (!req.ContainsKey("FromID") || !req.ContainsKey("ToID"))
            {
                return false;
            }

            string message = req["Message"].ToString();

            if (!UUID.TryParse(req["FromID"].ToString(), out fromID))
            {
                return false;
            }

            if (!UUID.TryParse(req["ToID"].ToString(), out toID))
            {
                return false;
            }

            IAgent agent;
            SceneInterface agentScene;
            if(!TryRootAgent(toID, out agent, out agentScene))
            {
                return false;
            }

            GridInstantMessage gim = new GridInstantMessage();
            if (!agentScene.AvatarNameService.TryGetValue(fromID, out gim.FromAgent))
            {
                return false;
            }

            gim.ToAgent = agent.Owner;
            gim.Message = message;
            gim.IMSessionID = fromID;
            gim.Dialog = GridInstantMessageDialog.FriendshipOffered;
            gim.RegionID = agentScene.ID;
            gim.ParentEstateID = agentScene.ParentEstateID;

            return agent.IMSend(gim);
        }

        bool TryRootAgent(UUID agentID, out IAgent agent, out SceneInterface agentScene)
        {
            agent = null;
            agentScene = null;
            foreach (SceneInterface scene in m_Scenes.Values)
            {
                if (scene.RootAgents.TryGetValue(agentID, out agent))
                {
                    agentScene = scene;
                    return true;
                }
            }
            return false;
        }
    }

    #region Service Factory
    [PluginName("SimFriendsHandler")]
    public class SimFriendsHandlerFactory : IPluginFactory
    {
        public SimFriendsHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new SimFriendsHandler();
        }
    }
    #endregion
}
