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
using SilverSim.ServiceInterfaces;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.Profile;
using SilverSim.Types;
using SilverSim.Types.Profile;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.BackendHandlers.Robust.Profile
{
    [Description("Robust CoreProfile/OpenSimProfile Protocol Server")]
    [PluginName("ProfileHandler")]
    public class ProfileServerHandler : IPlugin, IServiceURLsGetInterface
    {
        private ProfileServiceInterface m_ProfileService;
        private UserAccountServiceInterface m_UserAccountService;
        private readonly string m_ProfileServiceName;
        private readonly string m_UserAccountServiceName;
        private BaseHttpServer m_HttpServer;

        public ProfileServerHandler(IConfig ownSection)
        {
            m_ProfileServiceName = ownSection.GetString("ProfileService", "ProfileService");
            m_UserAccountServiceName = ownSection.GetString("UserAccountService", "UserAccountService");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_HttpServer = loader.HttpServer;
            HttpJson20RpcHandler jsonServer = loader.Json20RpcServer;
            HttpXmlRpcHandler xmlrpcServer = loader.XmlRpcServer;
            m_ProfileService = loader.GetService<ProfileServiceInterface>(m_ProfileServiceName);
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);

            jsonServer.Json20RpcMethods.Add("avatar_notes_update", Json_AvatarNotesUpdate);
            jsonServer.Json20RpcMethods.Add("avatar_properties_request", Json_AvatarPropertiesRequest);
            jsonServer.Json20RpcMethods.Add("avatar_properties_update", Json_AvatarPropertiesUpdate);
            jsonServer.Json20RpcMethods.Add("avatarclassifiedsrequest", Json_AvatarClassifiedsRequest);
            jsonServer.Json20RpcMethods.Add("avatarnotesrequest", Json_AvatarNotesRequest);
            jsonServer.Json20RpcMethods.Add("avatarpicksrequest", Json_AvatarPicksRequest);
            jsonServer.Json20RpcMethods.Add("classified_delete", Json_ClassifiedDelete);
            jsonServer.Json20RpcMethods.Add("classified_update", Json_ClassifiedUpdate);
            jsonServer.Json20RpcMethods.Add("classifieds_info_query", Json_ClassifiedsInfoQuery);
            jsonServer.Json20RpcMethods.Add("pick_info_request", Json_PickInfoRequest);
            jsonServer.Json20RpcMethods.Add("picks_delete", Json_PicksDelete);
            jsonServer.Json20RpcMethods.Add("picks_update", Json_PicksUpdate);
            jsonServer.Json20RpcMethods.Add("user_preferences_request", Json_UserPreferencesRequest);
            jsonServer.Json20RpcMethods.Add("user_preferences_update", Json_UserPreferencesUpdate);
            jsonServer.Json20RpcMethods.Add("image_assets_request", Json_ImageAssetsRequest);

            xmlrpcServer.XmlRpcMethods.Add("user_preferences_request", XmlRpc_UserPreferencesRequest);
            xmlrpcServer.XmlRpcMethods.Add("user_preferences_update", XmlRpc_UserPreferencesUpdate);
            xmlrpcServer.XmlRpcMethods.Add("avatar_interests_update", XmlRpc_AvatarInterestsUpdate);
            xmlrpcServer.XmlRpcMethods.Add("avatar_notes_update", XmlRpc_AvatarNotesUpdate);
            xmlrpcServer.XmlRpcMethods.Add("avatar_properties_request", XmlRpc_AvatarPropertiesRequest);
            xmlrpcServer.XmlRpcMethods.Add("avatar_properties_update", XmlRpc_AvatarPropertiesUpdate);
            xmlrpcServer.XmlRpcMethods.Add("avatarclassifiedsrequest", XmlRpc_AvatarClassifiedsRequest);
            xmlrpcServer.XmlRpcMethods.Add("avatarnotesrequest", XmlRpc_AvatarNotesRequest);
            xmlrpcServer.XmlRpcMethods.Add("avatarpicksrequest", XmlRpc_AvatarPicksRequest);
            xmlrpcServer.XmlRpcMethods.Add("classified_delete", XmlRpc_ClassifiedDelete);
            xmlrpcServer.XmlRpcMethods.Add("classified_update", XmlRpc_ClassifiedUpdate);
            xmlrpcServer.XmlRpcMethods.Add("classifieds_info_query", XmlRpc_ClassifiedsInfoQuery);
            xmlrpcServer.XmlRpcMethods.Add("pickinforequest", XmlRpc_PickInfoRequest);
            xmlrpcServer.XmlRpcMethods.Add("picks_delete", XmlRpc_PicksDelete);
            xmlrpcServer.XmlRpcMethods.Add("picks_update", XmlRpc_PicksUpdate);
        }

        #region CoreProfile V2
        private Map PropertiesToMap(ProfileProperties props) => new Map
        {
            { "UserId", props.User.ID.ToString() },
            { "WebUrl", props.WebUrl },
            { "ImageId", props.ImageID },
            { "AboutText", props.AboutText },
            { "FirstLifeImageId", props.FirstLifeImageID },
            { "FirstLifeText", props.FirstLifeText }
        };

        private Map ClassifiedToMap(ProfileClassified classified) => new Map
        {
            { "CreatorId", classified.Creator.ToString() },
            { "ParcelId", classified.ParcelID },
            { "SnapshotId", classified.SnapshotID },
            { "CreationDate", classified.CreationDate },
            { "ParentEstate", classified.ParentEstate },
            { "Flags", classified.Flags },
            { "Category", classified.Category },
            { "Price", classified.Price },
            { "Name", classified.Name },
            { "Description", classified.Description },
            { "SimName", classified.SimName },
            { "GlobalPos", classified.GlobalPos.ToString() },
            { "ParcelName", classified.ParcelName }
        };

        private Map PickToMap(ProfilePick pick) => new Map
        {
            { "PickId", pick.PickID },
            { "CreatorId", pick.Creator.ToString() },
            { "TopPick", pick.TopPick },
            { "Name", pick.Name },
            { "OriginalName", pick.OriginalName },
            { "Desc", pick.Description },
            { "ParcelId", pick.ParcelID },
            { "SnapshotId", pick.SnapshotID },
            { "User", pick.Creator.ToString() },
            { "SimName", pick.SimName },
            { "GlobalPos", pick.GlobalPosition.ToString() },
            { "SortOrder", pick.SortOrder },
            { "Enabled", pick.Enabled }
        };

        private IValue Json_ImageAssetsRequest(string method, IValue req)
        {
            Map reqdata = req as Map;
            UUID userId;
            if (reqdata == null ||
                !reqdata.TryGetValue("avatarId", out userId))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing parameters");
            }

            List<UUID> assetids = m_ProfileService.GetUserImageAssets(new UUI(userId));
            var resdata = new AnArray();
            foreach(UUID id in assetids)
            {
                resdata.Add(id);
            }
            return resdata;
        }

        private IValue Json_AvatarNotesUpdate(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID userId;
            UUID targetId;
            AString notes;
            if (reqdata == null ||
                !reqdata.TryGetValue("UserId", out userId) ||
                !reqdata.TryGetValue("TargetId", out targetId) ||
                !reqdata.TryGetValue("Notes",out notes))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing parameters");
            }

            m_ProfileService.Notes[new UUI(userId), new UUI(targetId)] = notes.ToString();
            return new Map
            {
                { "UserID", userId },
                { "TargetID", targetId },
                { "Notes", notes.ToString() }
            };
        }

        private IValue Json_AvatarPropertiesRequest(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID userId;
            if (reqdata == null || !reqdata.TryGetValue("UserId", out userId))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing UserId");
            }

            return PropertiesToMap(m_ProfileService.Properties[new UUI(userId)]);
        }

        private IValue Json_AvatarInterestsUpdate(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID userId;
            if (reqdata == null || !reqdata.TryGetValue("UserId", out userId))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing UserId");
            }

            ProfileProperties props = m_ProfileService.Properties[new UUI(userId)];
            AString stringval;
            Integer integerval;

            if(reqdata.TryGetValue("WantToMask", out integerval))
            {
                props.WantToMask = integerval.AsUInt;
            }
            if(reqdata.TryGetValue("WantToText", out stringval))
            {
                props.WantToText = stringval.ToString();
            }
            if (reqdata.TryGetValue("SkillsMask", out integerval))
            {
                props.SkillsMask = integerval.AsUInt;
            }
            if (reqdata.TryGetValue("SkillsText", out stringval))
            {
                props.SkillsText = stringval.ToString();
            }
            if(reqdata.TryGetValue("Language",out stringval))
            {
                props.Language = stringval.ToString();
            }
            m_ProfileService.Properties[new UUI(userId), ProfileServiceInterface.PropertiesUpdateFlags.Interests] = props;
            return new Map
            {
                { "UserId", userId },
                { "WantToMask", (int)props.WantToMask },
                { "WantToText", props.WantToText },
                { "SkillsMask", (int)props.SkillsMask },
                { "SkillsText", props.SkillsText },
                { "Language", props.Language }
            };
        }

        private IValue Json_AvatarPropertiesUpdate(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID userId;
            if (reqdata == null || !reqdata.TryGetValue("UserId", out userId))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing UserId");
            }

            ProfileProperties props = m_ProfileService.Properties[new UUI(userId)];
            AString stringval;
            if(reqdata.TryGetValue("WebUrl", out stringval))
            {
                props.WebUrl = stringval.ToString();
            }
            UUID id;
            if (reqdata.TryGetValue("ImageId", out id))
            {
                props.ImageID = id;
            }
            if(reqdata.TryGetValue("AboutText", out stringval))
            {
                props.AboutText = stringval.ToString();
            }
            if(reqdata.TryGetValue("FirstLifeImageId", out id))
            {
                props.FirstLifeImageID = id;
            }
            if(reqdata.TryGetValue("FirstLifeText", out stringval))
            {
                props.FirstLifeText = stringval.ToString();
            }
            m_ProfileService.Properties[new UUI(userId), ProfileServiceInterface.PropertiesUpdateFlags.Properties] = props;
            return PropertiesToMap(props);
        }

        private IValue Json_AvatarClassifiedsRequest(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID creatorId;
            if (reqdata == null || !reqdata.TryGetValue("creatorId", out creatorId))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing creatorId");
            }

            Dictionary<UUID, string> classifieds = m_ProfileService.Classifieds.GetClassifieds(new UUI(creatorId));
            var resdata = new AnArray();
            foreach(KeyValuePair<UUID, string> kvp in classifieds)
            {
                var r = new Map
                {
                    { "classifieduuid", kvp.Key },
                    { "name", kvp.Value }
                };
                resdata.Add(r);
            }
            return resdata;
        }

        private IValue Json_AvatarNotesRequest(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID userId;
            UUID targetId;
            if (reqdata == null || !reqdata.TryGetValue("UserId", out userId) ||
                !reqdata.TryGetValue("TargetId", out targetId))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing parameters");
            }

            string notes;
            if(!m_ProfileService.Notes.TryGetValue(new UUI(userId), new UUI(targetId), out notes))
            {
                notes = "";
            }
            return new Map
            {
                { "notes", notes }
            };
        }

        private IValue Json_AvatarPicksRequest(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID creatorId;
            if (reqdata == null || !reqdata.TryGetValue("creatorId", out creatorId))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing creatorId");
            }

            Dictionary<UUID, string> picks = m_ProfileService.Picks.GetPicks(new UUI(creatorId));
            var resdata = new AnArray();
            foreach(KeyValuePair<UUID, string> kvp in picks)
            {
                var r = new Map
                {
                    { "pickuuid", kvp.Key },
                    { "name", kvp.Value }
                };
                resdata.Add(r);
            }
            return resdata;
        }

        private IValue Json_ClassifiedDelete(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID classifiedID;
            if (reqdata == null || !reqdata.TryGetValue("ClassifiedId", out classifiedID))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing ClassifiedId");
            }

            try
            {
                m_ProfileService.Classifieds.Delete(classifiedID);
            }
            catch(Exception e)
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32604, e.Message);
            }
            return new AString("success");
        }

        private IValue Json_ClassifiedUpdate(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID classifiedID;
            UUID creatorID;
            if (reqdata == null || !reqdata.TryGetValue("ClassifiedId", out classifiedID) ||
                !reqdata.TryGetValue("CreatorId", out creatorID))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing ClassifiedId and/or CreatorId");
            }

            ProfileClassified classified;
            if(!m_ProfileService.Classifieds.TryGetValue(new UUI(creatorID), classifiedID, out classified))
            {
                classified = new ProfileClassified()
                {
                    ClassifiedID = classifiedID,
                    Creator = new UUI(creatorID)
                };
            }

            Date date;
            if(reqdata.TryGetValue("CreationDate", out date))
            {
                classified.CreationDate = date;
            }
            if (reqdata.TryGetValue("ExpirationDate", out date))
            {
                classified.ExpirationDate = date;
            }
            Integer integerval;
            if(reqdata.TryGetValue("Category", out integerval))
            {
                classified.Category = integerval;
            }
            AString stringval;
            if(reqdata.TryGetValue("Name", out stringval))
            {
                classified.Name = stringval.ToString();
            }
            if(reqdata.TryGetValue("Description", out stringval))
            {
                classified.Description = stringval.ToString();
            }
            UUID id;
            if(reqdata.TryGetValue("ParcelId", out id))
            {
                classified.ParcelID = id;
            }
            if(reqdata.TryGetValue("ParentEstate", out integerval))
            {
                classified.ParentEstate = integerval;
            }
            if(reqdata.TryGetValue("SnapshotId", out id))
            {
                classified.SnapshotID = id;
            }
            if(reqdata.TryGetValue("SimName", out stringval))
            {
                classified.SimName = stringval.ToString();
            }
            Vector3 pos;
            if(reqdata.TryGetValue("GlobalPos", out stringval) && Vector3.TryParse(stringval.ToString(), out pos))
            {
                classified.GlobalPos = pos;
            }
            if(reqdata.TryGetValue("ParcelName", out stringval))
            {
                classified.ParcelName = stringval.ToString();
            }
            if(reqdata.TryGetValue("Flags", out integerval))
            {
                classified.Flags = (byte)integerval;
            }
            if(reqdata.TryGetValue("Price", out integerval))
            {
                classified.Price = integerval;
            }
            try
            {
                m_ProfileService.Classifieds.Update(classified);
            }
            catch(Exception e)
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32604, e.Message);
            }
            return ClassifiedToMap(classified);
        }

        private IValue Json_ClassifiedsInfoQuery(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID classifiedId;
            if (reqdata == null || !reqdata.TryGetValue("ClassifiedId", out classifiedId))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing ClassifiedId");
            }

            ProfileClassified classified;
            try
            {
                classified = m_ProfileService.Classifieds[UUI.Unknown, classifiedId];
            }
            catch
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32604, "Not Found");
            }
            return ClassifiedToMap(classified);
        }

        private IValue Json_PickInfoRequest(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID pickID;
            UUID creatorID;
            if (reqdata == null || !reqdata.TryGetValue("PickId", out pickID) ||
                !reqdata.TryGetValue("CreatorId", out creatorID))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing PickId and/or CreatorId");
            }

            ProfilePick pick;
            try
            {
                pick = m_ProfileService.Picks[new UUI(creatorID), pickID];
            }
            catch(Exception e)
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32604, e.Message);
            }
            return PickToMap(pick);
        }

        private IValue Json_PicksDelete(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID pickID;
            if (reqdata == null || !reqdata.TryGetValue("pickId", out pickID))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing pickId");
            }

            try
            {
                m_ProfileService.Picks.Delete(pickID);
                return new AString("success");
            }
            catch(Exception e)
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32604, e.Message);
            }
        }

        private IValue Json_PicksUpdate(string method, IValue req)
        {
            var reqdata = req as Map;
            UUID pickID;
            UUID creatorID;
            if (reqdata == null || !reqdata.TryGetValue("PickId", out pickID) ||
                !reqdata.TryGetValue("CreatorId", out creatorID))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing PickId");
            }
            ProfilePick pick;
            if(!m_ProfileService.Picks.TryGetValue(new UUI(creatorID), pickID, out pick))
            {
                pick = new ProfilePick()
                {
                    PickID = pickID,
                    Creator = new UUI(creatorID)
                };
            }

            ABoolean boolval;
            AString stringval;

            if(reqdata.TryGetValue("TopPick", out boolval))
            {
                pick.TopPick = boolval;
            }

            if(reqdata.TryGetValue("OriginalName", out stringval))
            {
                pick.OriginalName = stringval.ToString();
            }

            if(reqdata.TryGetValue("Desc", out stringval))
            {
                pick.Description = stringval.ToString();
            }

            UUID id;
            if(reqdata.TryGetValue("ParcelId", out id))
            {
                pick.ParcelID = id;
            }

            if(reqdata.TryGetValue("SnapshotId", out id))
            {
                pick.SnapshotID = id;
            }

            if(reqdata.TryGetValue("SimName", out stringval))
            {
                pick.SimName = stringval.ToString();
            }

            Vector3 pos;
            if(reqdata.TryGetValue("GlobalPos", out stringval) && Vector3.TryParse(stringval.ToString(), out pos))
            {
                pick.GlobalPosition = pos;
            }
            Integer integerval;
            if(reqdata.TryGetValue("SortOrder", out integerval))
            {
                pick.SortOrder = integerval;
            }
            if(reqdata.TryGetValue("Enabled", out boolval))
            {
                pick.Enabled = boolval;
            }
            m_ProfileService.Picks.Update(pick);
            return PickToMap(pick);
        }

        private IValue Json_UserPreferencesRequest(string method, IValue req)
        {
            var reqdata = req as Map;
            if (reqdata == null || !reqdata.ContainsKey("UserId"))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing UserId");
            }
            UUID id;
            string data = reqdata["UserId"].ToString().Substring(0, 36);
            if (!UUID.TryParse(data, out id))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Invalid UserId");
            }

            ProfilePreferences prefs;
            if(!m_ProfileService.Preferences.TryGetValue(new UUI(id), out prefs))
            {
                prefs = new ProfilePreferences()
                {
                    User = new UUI(id),
                    Visible = true,
                    IMviaEmail = false
                };
            }

            return new Map
            {
                { "IMViaEmail", prefs.IMviaEmail },
                { "Visible", prefs.Visible },
                { "EMail", string.Empty }
            };
        }

        private IValue Json_UserPreferencesUpdate(string method, IValue req)
        {
            var reqdata = req as Map;
            if (reqdata == null || !reqdata.ContainsKey("UserId"))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Missing UserId");
            }
            UUID id;
            string data = reqdata["UserId"].ToString().Substring(0, 36);
            if (!UUID.TryParse(data, out id))
            {
                throw new HttpJson20RpcHandler.JSON20RpcException(-32602, "Invalid UserId");
            }

            ProfilePreferences prefs;
            if (!m_ProfileService.Preferences.TryGetValue(new UUI(id), out prefs))
            {
                prefs = new ProfilePreferences()
                {
                    User = new UUI(id),
                    Visible = true,
                    IMviaEmail = false
                };
            }

            bool b;
            if (reqdata.TryGetValue("IMViaEmail", out b))
            {
                prefs.IMviaEmail = b;
            }
            if (reqdata.TryGetValue("Visible", out b))
            {
                prefs.Visible = b;
            }

            m_ProfileService.Preferences[prefs.User] = prefs;

            return new Map
            {
                { "IMViaEmail", prefs.IMviaEmail },
                { "Visible", prefs.Visible },
                { "EMail", string.Empty }
            };
        }

        #endregion

        #region OpenSimProfile
        private bool ToBoolean(IValue v)
        {
            string s = v.ToString().ToLower();
            if(s == "true")
            {
                return true;
            }
            if(s == "false")
            {
                return false;
            }
            int i;
            return int.TryParse(s, out i) && i != 0;
        }

        private XmlRpc.XmlRpcResponse XmlRpc_UserPreferencesRequest(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID avatarid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("avatar_id", out avatarid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            ProfilePreferences prefs;
            if (!m_ProfileService.Preferences.TryGetValue(new UUI(avatarid), out prefs))
            {
                prefs = new ProfilePreferences();
                prefs.User.ID = avatarid;
                prefs.Visible = true;
            }

            var resdata = new Map
            {
                { "imviaemail", prefs.IMviaEmail },
                { "visible", prefs.Visible },
                { "email", string.Empty },
                { "success", true }
            };
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_UserPreferencesUpdate(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID avatarid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("avatar_id", out avatarid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            ProfilePreferences prefs;
            if(!m_ProfileService.Preferences.TryGetValue(new UUI(avatarid), out prefs))
            {
                prefs = new ProfilePreferences();
                prefs.User.ID = avatarid;
            }

            var resdata = new Map();
            try
            {
                prefs.IMviaEmail = ToBoolean(structParam["imViaEmail"]);
                prefs.Visible = ToBoolean(structParam["visible"]);
                m_ProfileService.Preferences[new UUI(avatarid)] = prefs;
                resdata.Add("success", true);
            }
            catch(Exception e)
            {
                resdata.Add("errorMessage", e.Message);
            }

            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_AvatarInterestsUpdate(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID avatarid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("avatar_id", out avatarid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            var resdata = new Map();
            ProfileProperties props = m_ProfileService.Properties[new UUI(avatarid)];
            try
            {
                props.WantToMask = structParam["wantmask"].AsUInt;
                props.WantToText = structParam["wanttext"].ToString();
                props.SkillsMask = structParam["skillsmask"].AsUInt;
                props.SkillsText = structParam["skillstext"].ToString();
                props.Language = structParam["languages"].ToString();
                m_ProfileService.Properties[new UUI(avatarid), ProfileServiceInterface.PropertiesUpdateFlags.Interests] = props;
                resdata.Add("success", true);
            }
            catch(Exception e)
            {
                resdata.Add("errorMessage", e.Message);
            }

            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_AvatarNotesUpdate(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID avatarid;
            UUID targetid;
            AString stringval;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("avatar_id", out avatarid) ||
                !structParam.TryGetValue("target_id", out targetid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            var resdata = new Map();
            if (structParam.TryGetValue("notes", out stringval) && stringval.ToString().Length != 0)
            {
                try
                {
                    m_ProfileService.Notes[new UUI(avatarid), new UUI(targetid)] = stringval.ToString();
                    resdata.Add("success", true);
                }
                catch(Exception e)
                {
                    resdata.Add("errorMessage", e.Message);
                }
            }
            else
            {
                try
                {
                    m_ProfileService.Notes[new UUI(avatarid), new UUI(targetid)] = string.Empty;
                    resdata.Add("success", true);
                }
                catch (Exception e)
                {
                    resdata.Add("errorMessage", e.Message);
                }
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_AvatarPropertiesRequest(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID avatarid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("avatar_id", out avatarid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            ProfileProperties props = m_ProfileService.Properties[new UUI(avatarid)];
            var resdata = new Map
            {
                { "Partner", props.Partner.ID },
                { "ProfileUrl", props.WebUrl },
                { "wantmask", (int)props.WantToMask },
                { "wanttext", props.WantToText },
                { "skillsmask", (int)props.SkillsMask },
                { "skillstext", props.SkillsText },
                { "languages", props.Language },
                { "Image", props.ImageID },
                { "AboutText", props.AboutText },
                { "FirstLifeImage", props.FirstLifeImageID },
                { "FirstLifeAboutText", props.FirstLifeText },
                { "success", true }
            };
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_AvatarPropertiesUpdate(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID avatarid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("avatar_id", out avatarid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            ProfileProperties props;
            try
            {
                props = m_ProfileService.Properties[new UUI(avatarid)];
            }
            catch
            {
                props = new ProfileProperties();
                props.User.ID = avatarid;
            }
            var resdata = new Map();
            try
            {
                props.WebUrl = structParam["ProfileUrl"].ToString();
                props.ImageID = structParam["Image"].AsUUID;
                props.AboutText = structParam["AboutText"].ToString();
                props.FirstLifeImageID = structParam["FirstLifeImage"].AsUUID;
                props.FirstLifeText = structParam["FirstLifeAboutText"].ToString();
                m_ProfileService.Properties[new UUI(avatarid), ProfileServiceInterface.PropertiesUpdateFlags.Properties] = props;
                resdata.Add("success", true);
            }
            catch(Exception e)
            {
                resdata.Add("errorMessage", e.Message);
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_AvatarClassifiedsRequest(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID avatarid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("uuid", out avatarid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            Dictionary<UUID, string> classifieds = m_ProfileService.Classifieds.GetClassifieds(new UUI(avatarid));
            var resarray = new AnArray();
            foreach(KeyValuePair<UUID, string> kvp in classifieds)
            {
                var r = new Map
                {
                    { "classifiedid", kvp.Key },
                    { "name", kvp.Value }
                };
                resarray.Add(r);
            }
            var resdata = new Map
            {
                ["data"] = resarray
            };
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_AvatarNotesRequest(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID avatarid;
            UUID targetid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("avatar_id", out avatarid) ||
                !structParam.TryGetValue("uuid", out targetid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            string notes;
            var resdata = new Map();
            if(m_ProfileService.Notes.TryGetValue(new UUI(avatarid), new UUI(targetid), out notes))
            {
                resdata.Add("notes", notes);
                resdata.Add("success", true);
            }
            else
            {
                resdata.Add("success", false);
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_AvatarPicksRequest(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID avatarid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("uuid", out avatarid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            Dictionary<UUID, string> picks = m_ProfileService.Picks.GetPicks(new UUI(avatarid));
            var pickdata = new AnArray();
            foreach(KeyValuePair<UUID, string> kvp in picks)
            {
                var pickinfo = new Map
                {
                    { "pickid", kvp.Key },
                    { "name", kvp.Value }
                };
                pickdata.Add(pickinfo);
            }
            var resdata = new Map
            {
                ["data"] = pickdata
            };
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_ClassifiedDelete(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID classifiedid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("classifiedID", out classifiedid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            var resdata = new Map();
            try
            {
                m_ProfileService.Classifieds.Delete(classifiedid);
                resdata.Add("success", true);
            }
            catch(Exception e)
            {
                resdata.Add("errorMessage", e.Message);
            }

            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_ClassifiedUpdate(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID classifiedid;
            UUID creatorid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("classifiedUUID", out classifiedid) ||
                !structParam.TryGetValue("creatorUUID", out creatorid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            ProfileClassified classified;
            if(!m_ProfileService.Classifieds.TryGetValue(new UUI(creatorid), classifiedid, out classified))
            {
                classified = new ProfileClassified();
                classified.Creator.ID = creatorid;
                classified.ClassifiedID = classifiedid;
                classified.CreationDate = Date.Now;
            }

            try
            {
                classified.Category = structParam["category"].AsInt;
                classified.Name = structParam["name"].ToString();
                classified.Description = structParam["description"].ToString();
                classified.ParcelID = structParam["parcelUUID"].AsUUID;
                classified.ParentEstate = structParam["ParentEstate"].AsInt;
                classified.SnapshotID = structParam["snapshotUUID"].AsUUID;
                classified.SimName = structParam["sim_name"].ToString();
                classified.GlobalPos = Vector3.Parse(structParam["globalpos"].ToString());
                classified.ParcelName = structParam["parcelname"].ToString();
                classified.Flags = (byte)structParam["classifiedFlags"].AsInt;
                classified.Price = structParam["classifiedPrice"].AsInt;
                if((classified.Flags & 76) == 0)
                {
                    classified.Flags |= 4;
                }
                if((classified.Flags & 32) != 0)
                {
                    classified.ExpirationDate = Date.UnixTimeToDateTime(Date.Now.AsULong + (ulong)7 * 24 * 3600);
                }
                else
                {
                    classified.ExpirationDate = Date.UnixTimeToDateTime(Date.Now.AsULong + (ulong)365 * 24 * 3600);
                }
            }
            catch
            {
                throw new XmlRpc.XmlRpcFaultException(-32604, "Missing parameters");
            }

            var resdata = new Map();
            try
            {
                m_ProfileService.Classifieds.Update(classified);
                resdata.Add("success", true);
            }
            catch(Exception e)
            {
                resdata.Add("errorMessage", e.Message);
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_ClassifiedsInfoQuery(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID classifiedid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("classifiedID", out classifiedid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            ProfileClassified classified;
            var resdata = new Map();
            if (m_ProfileService.Classifieds.TryGetValue(UUI.Unknown, classifiedid, out classified))
            {
                resdata.Add("classifieduuid", classified.ClassifiedID);
                resdata.Add("creatoruuid", classified.Creator.ID);
                resdata.Add("creationdate", classified.CreationDate);
                resdata.Add("expirationdate", classified.ExpirationDate);
                resdata.Add("category", classified.Category);
                resdata.Add("name", classified.Name);
                resdata.Add("description", classified.Description);
                resdata.Add("parceluuid", classified.ParcelID);
                resdata.Add("parentestate", classified.ParentEstate);
                resdata.Add("snapshotuuid", classified.SnapshotID);
                resdata.Add("simname", classified.SimName);
                resdata.Add("posglobal", classified.GlobalPos.ToString());
                resdata.Add("parcelname", classified.ParcelName);
                resdata.Add("classifiedflags", classified.Flags);
                resdata.Add("priceforlisting", classified.Price);
            }
            else
            {
                resdata.Add("errorMessage", "Not found");
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_PickInfoRequest(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID pickid;
            UUID avatarid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("avatar_id",out avatarid) ||
                !structParam.TryGetValue("pick_id", out pickid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }
            ProfilePick pick;
            var resdata = new Map();
            if(m_ProfileService.Picks.TryGetValue(new UUI(avatarid), pickid, out pick))
            {
                resdata.Add("pickuuid", pick.PickID);
                resdata.Add("creatoruuid", pick.Creator.ID);
                resdata.Add("parceluuid", pick.ParcelID);
                resdata.Add("snapshotuuid", pick.SnapshotID);
                resdata.Add("posglobal", pick.GlobalPosition.ToString());
                resdata.Add("toppick", pick.TopPick ? "True" : "False");
                resdata.Add("enabled", pick.Enabled ? "True" : "False");
                resdata.Add("name", pick.Name);
                resdata.Add("description", pick.Description);
            }
            else
            {
                resdata.Add("errorMessage", "not found");
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_PicksDelete(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            UUID pickid;
            if (!req.Params.TryGetValue(0, out structParam) ||
                !structParam.TryGetValue("pick_id", out pickid))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }

            var resdata = new Map();
            try
            {
                m_ProfileService.Picks.Delete(pickid);
                resdata.Add("success", true);
            }
            catch (Exception e)
            {
                resdata.Add("success", false);
                resdata.Add("errorMessage", e.Message);
            }
            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        private XmlRpc.XmlRpcResponse XmlRpc_PicksUpdate(XmlRpc.XmlRpcRequest req)
        {
            Map structParam;
            if(!req.Params.TryGetValue(0, out structParam))
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing struct param");
            }
            try
            {
                var pick = new ProfilePick()
                {
                    PickID = structParam["pick_id"].AsUUID,
                    Creator = new UUI(structParam["creator_id"].AsUUID),
                    TopPick = ToBoolean(structParam["TopPick"]),
                    Name = structParam["Name"].ToString(),
                    OriginalName = structParam["name"].ToString(),
                    Description = structParam["desc"].ToString(),
                    ParcelID = structParam["parcel_uuid"].AsUUID,
                    SnapshotID = structParam["snapshot_uuid"].AsUUID,
                    SimName = structParam["sim_name"].ToString(),
                    GlobalPosition = Vector3.Parse(structParam["pos_global"].ToString()),
                    SortOrder = structParam["sort_order"].AsInt,
                    Enabled = ToBoolean(structParam["enabled"])
                };
                m_ProfileService.Picks.Update(pick);
                var resdata = new Map
                {
                    { "success", true }
                };
                return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
            }
            catch
            {
                throw new XmlRpc.XmlRpcFaultException(-32602, "Missing parameter");
            }
        }
        #endregion

        public void GetServiceURLs(Dictionary<string, string> dict)
        {
            dict["ProfileServerURI"] = m_HttpServer.ServerURI;
        }
    }
}
