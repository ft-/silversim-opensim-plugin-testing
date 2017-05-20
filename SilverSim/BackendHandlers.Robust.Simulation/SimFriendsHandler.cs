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

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Agent;
using SilverSim.Scene.Types.Scene;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Friends;
using SilverSim.Types.IM;
using SilverSim.Types.Inventory;
using SilverSim.Types.StructuredData.REST;
using SilverSim.Viewer.Messages.Friend;
using SilverSim.Viewer.Messages.Inventory;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Simulation
{
    [Description("Friends Status Receive Handler")]
    public class SimFriendsHandler : IPlugin, IPluginShutdown
    {
        private BaseHttpServer m_HttpServer;
        private SceneList m_Scenes;

        public ShutdownOrder ShutdownOrder => ShutdownOrder.Any;

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

        private void FriendsHandler(HttpRequest httpreq)
        {
            Dictionary<string, object> req;
            try
            {
                req = REST.ParseREST(httpreq.Body);
            }
            catch
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest);
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
                httpreq.ErrorResponse(HttpStatusCode.InternalServerError);
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

        private bool HandleFriendStatus(HttpRequest httpreq, Dictionary<string, object> req)
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
                        var m = new OnlineNotification();
                        m.AgentIDs.Add(fromID);
                        agent.SendMessageAlways(m, agentScene.ID);
                    }
                }
                else
                {
                    if (fStat.IsOnline)
                    {
                        fStat.IsOnline = false;
                        var m = new OfflineNotification();
                        m.AgentIDs.Add(fromID);
                        agent.SendMessageAlways(m, agentScene.ID);
                    }
                }
            }

            return true;
        }

        private bool HandleGrantRights(HttpRequest httpreq, Dictionary<string, object> req)
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
                var m = new ChangeUserRights()
                {
                    AgentID = agent.Owner.ID,
                    SessionID = agent.Session.SessionID
                };
                var r = new ChangeUserRights.RightsEntry()
                {
                    AgentRelated = gim.FromAgent.ID,
                    RelatedRights = (FriendRightFlags)newRights
                };
                m.Rights.Add(r);

                agent.SendMessageAlways(m, agentScene.ID);
            }

            return true;
        }

        private bool HandleFriendshipTerminated(HttpRequest httpreq, Dictionary<string, object> req)
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

            var m = new TerminateFriendship()
            {
                AgentID = agent.Owner.ID,
                SessionID = agent.Session.SessionID,
                OtherID = fromID
            };
            agent.SendMessageAlways(m, agentScene.ID);

            agent.KnownFriends.Remove(fromID);

            return true;
        }

        private bool HandleFriendshipDenied(HttpRequest httpreq, Dictionary<string, object> req)
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

            var gim = new GridInstantMessage();
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

        private bool HandleFriendshipApproved(HttpRequest httpreq, Dictionary<string, object> req)
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

            var gim = new GridInstantMessage();
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
                var item = new InventoryItem()
                {
                    AssetID = UUID.Zero,
                    AssetType = AssetType.CallingCard,
                    ID = UUID.Random,
                    Creator = gim.FromAgent,
                    Owner = gim.ToAgent,
                    Group = UGI.Unknown,
                    IsGroupOwned = false,
                    ParentFolderID = folder.ID,

                    CreationDate = Date.Now,
                    InventoryType = InventoryType.CallingCard,
                    Flags = 0,

                    Name = gim.ToAgent.FullName,
                    Description = ""
                };
                item.SaleInfo.Price = 10;
                item.SaleInfo.Type = InventoryItem.SaleInfoData.SaleType.NoSale;

                item.Permissions.Base = InventoryPermissionsMask.Copy | InventoryPermissionsMask.Modify;
                item.Permissions.EveryOne = InventoryPermissionsMask.None;
                item.Permissions.Current = item.Permissions.Base;
                item.Permissions.NextOwner = InventoryPermissionsMask.Copy | InventoryPermissionsMask.Modify;

                inventoryService.Item.Add(item);

                var m = new UpdateCreateInventoryItem(toID, true, UUID.Zero, item, 0);
                m.AddItem(item, 0);
                agent.SendMessageAlways(m, agentScene.ID);
            }

            if(agent.FriendsService != null)
            {
                FriendInfo fi;
                if(agent.FriendsService.TryGetValue(gim.ToAgent, gim.FromAgent, out fi))
                {
                    var fStat = new FriendStatus(fi)
                    {
                        IsOnline = true
                    };
                    agent.KnownFriends[fromID] = fStat;

                    if((fStat.UserGivenFlags & FriendRightFlags.SeeOnline) != 0)
                    {
                        var notification = new OnlineNotification();
                        notification.AgentIDs.Add(fromID);
                        agent.SendMessageAlways(notification, agentScene.ID);
                    }
                }
            }

            return agent.IMSend(gim);
        }

        private bool HandleFriendshipOffered(HttpRequest httpreq, Dictionary<string, object> req)
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

            var gim = new GridInstantMessage();
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

        private bool TryRootAgent(UUID agentID, out IAgent agent, out SceneInterface agentScene)
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
        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection) =>
            new SimFriendsHandler();
    }
    #endregion
}
