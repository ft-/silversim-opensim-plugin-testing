// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.Rpc;
using SilverSim.Types;
using SilverSim.Types.Profile;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.OpenSim.Profile
{
    public partial class ProfileConnector
    {
        public class RobustClassifiedsConnector : IClassifiedsInterface
        {
            string m_Uri;
            ProfileConnector m_Connector;

            public RobustClassifiedsConnector(ProfileConnector connector, string uri)
            {
                m_Connector = connector;
                m_Uri = uri;
            }

            public Dictionary<UUID, string> getClassifieds(UUI user)
            {
                Dictionary<UUID, string> data = new Dictionary<UUID, string>();
                Map m = new Map();
                m["creatorId"] = user.ID;
                AnArray reslist = (AnArray)RPC.DoJson20RpcRequest(m_Uri, "avatarclassifiedsrequest", (string)UUID.Random, m, m_Connector.TimeoutMs);
                foreach(IValue iv in reslist)
                {
                    Map c = (Map)iv;
                    data[c["classifieduuid"].AsUUID] = c["name"].ToString();
                }
                return data;
            }

            public ProfileClassified this[UUI user, UUID id]
            {
                get 
                {
                    ProfileClassified classified = new ProfileClassified();
                    Map m = new Map();
                    m["ClassifiedId"] = id;
                    Map reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "classifieds_info_query", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    classified.ClassifiedID = id;
                    classified.Creator.ID = reslist["CreatorId"].AsUUID;
                    classified.CreationDate = Date.UnixTimeToDateTime(reslist["CreationDate"].AsULong);
                    classified.ExpirationDate = Date.UnixTimeToDateTime(reslist["ExpirationDate"].AsULong);
                    classified.Category = reslist["Category"].AsInt;
                    classified.Name = reslist["Name"].ToString();
                    classified.Description = reslist["Description"].ToString();
                    classified.ParcelID = reslist["ParcelId"].AsUUID;
                    classified.ParentEstate = reslist["ParentEstate"].AsInt;
                    classified.SnapshotID = reslist["SnapshotId"].AsUUID;
                    classified.SimName = reslist["SimName"].ToString();
                    classified.GlobalPos = reslist["GlobalPos"].AsVector3;
                    classified.ParcelName = reslist["ParcelName"].ToString();
                    classified.Flags = (byte)reslist["Flags"].AsUInt;
                    classified.Price = reslist["Price"].AsInt;
                    return classified;
                }
            }


            public void Update(ProfileClassified classified)
            {
                Map m = new Map();
                m.Add("ClassifiedId", classified.ClassifiedID);
                m.Add("CreatorId", classified.Creator.ID);
                m.Add("CreationDate", classified.CreationDate.AsInt);
                m.Add("ExpirationDate", classified.ExpirationDate.AsInt);
                m.Add("Category", classified.Category);
                m.Add("Name", classified.Name);
                m.Add("Description", classified.Description);
                m.Add("ParcelId", classified.ParcelID);
                m.Add("ParentEstate", classified.ParentEstate);
                m.Add("SnapshotId", classified.SnapshotID);
                m.Add("SimName", classified.SimName);
                m.Add("GlobalPos", classified.GlobalPos);
                m.Add("ParcelName", classified.ParcelName);
                m.Add("Flags", (int)classified.Flags);
                m.Add("Price", classified.Price);

                RPC.DoJson20RpcRequest(m_Uri, "classified_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
            }

            public void Delete(UUID id)
            {
                Dictionary<UUID, string> data = new Dictionary<UUID, string>();
                Map m = new Map();
                m["ClassifiedId"] = id;
                RPC.DoJson20RpcRequest(m_Uri, "classified_delete", (string)UUID.Random, m, m_Connector.TimeoutMs);
            }
        }

        public class RobustPicksConnector : IPicksInterface
        {
            string m_Uri;
            ProfileConnector m_Connector;

            public RobustPicksConnector(ProfileConnector connector, string uri)
            {
                m_Uri = uri;
                m_Connector = connector;
            }

            public Dictionary<UUID, string> getPicks(UUI user)
            {
                Dictionary<UUID, string> data = new Dictionary<UUID, string>();
                Map m = new Map();
                m["creatorId"] = user.ID;
                AnArray reslist = (AnArray)RPC.DoJson20RpcRequest(m_Uri, "avatarpicksrequest", (string)UUID.Random, m, m_Connector.TimeoutMs);
                foreach (IValue iv in reslist)
                {
                    Map c = (Map)iv;
                    data[c["pickuuid"].AsUUID] = c["name"].ToString();
                }
                return data;
            }

            public ProfilePick this[UUI user, UUID id]
            {
                get
                {
                    ProfilePick pick = new ProfilePick();
                    Map m = new Map();
                    m["ClassifiedId"] = id;
                    m["CreatorId"] = user.ID;
                    Map reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "pickinforequest", (string)UUID.Random, m, m_Connector.TimeoutMs);

                    pick.PickID = reslist["PickId"].AsUUID;
                    pick.Creator = user;
                    pick.TopPick = (ABoolean)reslist["TopPick"];
                    pick.Name = reslist["Name"].ToString();
                    pick.OriginalName = reslist["OriginalName"].ToString();
                    pick.Description = reslist["Desc"].ToString();
                    pick.ParcelID = reslist["ParcelId"].AsUUID;
                    pick.SnapshotID = reslist["SnapshotId"].AsUUID;
                    pick.ParcelName = reslist["ParcelName"].ToString();
                    pick.SimName = reslist["SimName"].ToString();
                    pick.GlobalPosition = reslist["GlobalPos"].AsVector3;
                    pick.SortOrder = reslist["SortOrder"].AsInt;
                    pick.Enabled = (ABoolean)reslist["Enabled"];
                    if (reslist.ContainsKey("Gatekeeper"))
                    {
                        pick.GatekeeperURI = reslist["Gatekeeper"].ToString();
                    }
                    return pick;
                }
            }


            public void Update(ProfilePick pick)
            {
                Map m = new Map();
                m.Add("PickId", pick.PickID);
                m.Add("CreatorId", pick.Creator.ID);
                m.Add("TopPick", pick.TopPick);
                m.Add("Name", pick.Name);
                m.Add("Desc", pick.Description);
                m.Add("ParcelId", pick.ParcelID);
                m.Add("SnapshotId", pick.SnapshotID);
                m.Add("ParcelName", pick.ParcelName);
                m.Add("SimName", pick.SimName);
                m.Add("Gatekeeper", pick.GatekeeperURI);
                m.Add("GlobalPos", pick.GlobalPosition.ToString());
                m.Add("SortOrder", pick.SortOrder);
                m.Add("Enabled", pick.Enabled);

                RPC.DoJson20RpcRequest(m_Uri, "picks_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
            }

            public void Delete(UUID id)
            {
                Dictionary<UUID, string> data = new Dictionary<UUID, string>();
                Map m = new Map();
                m["pickId"] = id;
                RPC.DoJson20RpcRequest(m_Uri, "picks_delete", (string)UUID.Random, m, m_Connector.TimeoutMs);
            }
        }

        public class RobustNotesConnector : INotesInterface
        {
            string m_Uri;
            ProfileConnector m_Connector;

            public RobustNotesConnector(ProfileConnector connector, string uri)
            {
                m_Uri = uri;
                m_Connector = connector;
            }

            public string this[UUI user, UUI target]
            {
                get
                {
                    Map m = new Map();
                    m["UserId"] = user.ID;
                    m["TargetId"] = target.ID;
                    Map reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "avatarnotesrequest", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    return reslist["Notes"].ToString();
                }
                set
                {
                    Map m = new Map();
                    m["UserId"] = user.ID;
                    m["TargetId"] = target.ID;
                    m.Add("Notes", value);
                    RPC.DoJson20RpcRequest(m_Uri, "avatar_notes_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
                }
            }
        }

        public class RobustUserPreferencesConnector : IUserPreferencesInterface
        {
            string m_Uri;
            ProfileConnector m_Connector;

            public RobustUserPreferencesConnector(ProfileConnector connector, string uri)
            {
                m_Uri = uri;
                m_Connector = connector;
            }

            public ProfilePreferences this[UUI user]
            {
                get
                {
                    ProfilePreferences prefs = new ProfilePreferences();
                    Map m = new Map();
                    m["UserId"] = user.ID;
                    Map reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "user_preferences_request", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    prefs.User = user;
                    prefs.IMviaEmail = (ABoolean)reslist["IMViaEmail"];
                    prefs.Visible = (ABoolean)reslist["Visible"];
                    return prefs;
                }
                set
                {
                    Map m = new Map();
                    m["UserId"] = user.ID;
                    m.Add("IMViaEmail", value.IMviaEmail);
                    m.Add("Visible", value.Visible);
                    RPC.DoJson20RpcRequest(m_Uri, "user_preferences_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
                }
            }
        }

        public class RobustPropertiesConnector : IPropertiesInterface
        {
            string m_Uri;
            ProfileConnector m_Connector;

            public RobustPropertiesConnector(ProfileConnector connector, string uri)
            {
                m_Connector = connector;
                m_Uri = uri;
            }

            public ProfileProperties this[UUI user]
            {
                get
                {
                    Map m = new Map();
                    m.Add("UserId", user.ID);
                    Map reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "avatar_properties_request", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    ProfileProperties props = new ProfileProperties();

                    props.User = user;
                    props.Partner = UUI.Unknown;
                    props.Partner.ID = reslist["PartnerId"].AsUUID;
                    props.PublishProfile = (ABoolean)reslist["PublishProfile"];
                    props.PublishMature = (ABoolean)reslist["PublishMature"];
                    props.WebUrl = reslist["WebUrl"].ToString();
                    props.WantToMask = reslist["WantToMask"].AsUInt;
                    props.WantToText = reslist["WantToText"].ToString();
                    props.SkillsMask = reslist["SkillsMask"].AsUInt;
                    props.SkillsText = reslist["SkillsText"].ToString();
                    props.Language = reslist["Language"].ToString();
                    props.ImageID = reslist["ImageId"].AsUUID;
                    props.AboutText = reslist["AboutText"].ToString();
                    props.FirstLifeImageID = reslist["FirstLifeImageId"].AsUUID;
                    props.FirstLifeText = reslist["FirstLifeText"].ToString();

                    return props;
                }
            }

            public ProfileProperties this[UUI user, PropertiesUpdateFlags flags] 
            { 
                set
                {
                    Map m = new Map();
                    m.Add("UserId", user.ID);
                    m.Add("PartnerId", value.Partner.ID);
                    m.Add("PublishProfile", value.PublishProfile);
                    m.Add("PublishMature", value.PublishMature);
                    m.Add("WebUrl", value.WebUrl);
                    m.Add("WantToMask", (int)value.WantToMask);
                    m.Add("WantToText", value.WantToText);
                    m.Add("SkillsMask", (int)value.SkillsMask);
                    m.Add("SkillsText", value.SkillsText);
                    m.Add("Language", value.Language);
                    m.Add("ImageId", value.ImageID);
                    m.Add("AboutText", value.AboutText);
                    m.Add("FirstLifeImageId", value.FirstLifeImageID);
                    m.Add("FirstLifeText", value.FirstLifeText);
                    if((flags & PropertiesUpdateFlags.Interests) != 0)
                    {
                        RPC.DoJson20RpcRequest(m_Uri, "avatar_interests_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    }
                    if ((flags & PropertiesUpdateFlags.Interests) != 0)
                    {
                        RPC.DoJson20RpcRequest(m_Uri, "avatar_properties_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    }
                }
            }
        }
    }
}
