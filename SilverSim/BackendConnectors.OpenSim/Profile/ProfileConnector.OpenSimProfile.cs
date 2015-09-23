// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.Rpc;
using SilverSim.Types;
using SilverSim.Types.Profile;
using SilverSim.Types.StructuredData.XMLRPC;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.OpenSim.Profile
{
    public partial class ProfileConnector
    {
        public class OpenSimProfileConnector
        {
            protected string m_Uri;
            protected ProfileConnector m_Connector;

            public OpenSimProfileConnector(ProfileConnector connector, string uri)
            {
                m_Uri = uri;
                m_Connector = connector;
            }

            protected IValue OpenSimXmlRpcCall(string methodName, Map structparam)
            {
                XMLRPC.XmlRpcRequest req = new XMLRPC.XmlRpcRequest();
                req.MethodName = methodName;
                req.Params.Add(structparam);
                XMLRPC.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, m_Connector.TimeoutMs);
                if (!(res.ReturnValue is Map))
                {
                    throw new Exception("Unexpected OpenSimProfile return value");
                }
                Map p = (Map)res.ReturnValue;
                if (!p.ContainsKey("success"))
                {
                    throw new Exception("Unexpected OpenSimProfile return value");
                }

                if (p["success"].ToString().ToLower() != "true")
                {
                    throw new KeyNotFoundException();
                }
                if (p.ContainsKey("data"))
                {
                    return p["data"];
                }
                else
                {
                    return null; /* some calls have no data */
                }
            }
        }

        public class OpenSimClassifiedsConnector : OpenSimProfileConnector, IClassifiedsInterface
        {

            public OpenSimClassifiedsConnector(ProfileConnector connector, string uri)
                : base(connector,uri)
            {
            }

            public Dictionary<UUID, string> getClassifieds(UUI user)
            {
                Map map = new Map();
                map.Add("uuid", user.ID);
                AnArray res = (AnArray)OpenSimXmlRpcCall("avatarclassifiedsrequest", map);
                Dictionary<UUID, string> classifieds = new Dictionary<UUID, string>();
                foreach(IValue iv in res)
                {
                    Map m = (Map)iv;
                    classifieds.Add(m["classifiedid"].AsUUID, m["name"].ToString());
                }
                return classifieds;
            }

            public ProfileClassified this[UUI user, UUID id]
            {
                get
                {
                    Map map = new Map();
                    map.Add("classifiedID", user.ID);
                    Map res = (Map)(((AnArray)OpenSimXmlRpcCall("classifieds_info_query", map))[0]);

                    ProfileClassified classified = new ProfileClassified();
                    classified.ClassifiedID = res["classifieduuid"].AsUUID;
                    classified.Creator.ID = res["creatoruuid"].AsUUID;
                    classified.CreationDate = Date.UnixTimeToDateTime(res["creationdate"].AsULong);
                    classified.ExpirationDate = Date.UnixTimeToDateTime(res["expirationdate"].AsULong);
                    classified.Category = res["category"].AsInt;
                    classified.Name = res["name"].ToString();
                    classified.Description = res["description"].ToString();
                    classified.ParcelID = res["parceluuid"].AsUUID;
                    classified.ParentEstate = res["parentestate"].AsInt;
                    classified.SnapshotID = res["snapshotuuid"].AsUUID;
                    classified.SimName = res["simname"].ToString();
                    classified.GlobalPos = res["posglobal"].AsVector3;
                    classified.ParcelName = res["parcelname"].ToString();
                    classified.Flags = (byte)res["classifiedflags"].AsUInt;
                    classified.Price = res["priceforlisting"].AsInt;
                    return classified;
                }
            }


            public void Update(ProfileClassified classified)
            {
                Map map = new Map();
                map.Add("parcelname", classified.ParcelName);
                map.Add("creatorUUID", classified.Creator.ID);
                map.Add("classifiedUUID", classified.ClassifiedID);
                map.Add("category", ((int)classified.Category).ToString());
                map.Add("name", classified.Name);
                map.Add("description", classified.Description);
                map.Add("parentestate", classified.ParentEstate.ToString());
                map.Add("snapshotUUID", classified.SnapshotID);
                map.Add("sim_name", classified.SimName);
                map.Add("globalpos", classified.GlobalPos.ToString());
                map.Add("classifiedFlags", ((uint)classified.Flags).ToString());
                map.Add("classifiedPrice", classified.Price.ToString());
                map.Add("parcelUUID", classified.ParcelID.ToString());
                map.Add("pos_global", classified.GlobalPos.ToString());
                OpenSimXmlRpcCall("classified_update", map);
            }

            public void Delete(UUID id)
            {
                Map map = new Map();
                map["classifiedID"] = id;
                OpenSimXmlRpcCall("classified_delete", map);
            }
        }

        public class OpenSimPicksConnector : OpenSimProfileConnector, IPicksInterface
        {
            public OpenSimPicksConnector(ProfileConnector connector, string uri)
                : base(connector, uri)
            {

            }

            public Dictionary<UUID, string> getPicks(UUI user)
            {
                Map map = new Map();
                map.Add("uuid", user.ID);
                AnArray res = (AnArray)OpenSimXmlRpcCall("avatarpicksrequest", map);
                Dictionary<UUID, string> classifieds = new Dictionary<UUID, string>();
                foreach (IValue iv in res)
                {
                    Map m = (Map)iv;
                    classifieds.Add(m["pickid"].AsUUID, m["name"].ToString());
                }
                return classifieds;
            }

            public ProfilePick this[UUI user, UUID id]
            {
                get 
                {
                    Map map = new Map();
                    map.Add("avatar_id", user.ID);
                    map.Add("pick_id", id);
                    Map res = (Map)(((AnArray)OpenSimXmlRpcCall("pickinforequest", map))[0]);
                    ProfilePick pick = new ProfilePick();
                    pick.PickID = res["pickuuid"].AsUUID;
                    pick.Creator.ID = res["creatoruuid"].AsUUID;
                    pick.TopPick = Convert.ToBoolean(res["toppick"].ToString());
                    pick.ParcelID = res["parceluuid"].AsUUID;
                    pick.Name = res["name"].ToString();
                    pick.Description = res["description"].ToString();
                    pick.SnapshotID = res["snapshotuuid"].AsUUID;
                    pick.OriginalName = res["originalname"].ToString();
                    pick.SimName = res["simname"].ToString();
                    pick.GlobalPosition = res["posglobal"].AsVector3;
                    pick.SortOrder = res["sortorder"].AsInt;
                    pick.Enabled = Convert.ToBoolean(res["enabled"].ToString());
                    return pick;
                }
            }


            public void Update(ProfilePick pick)
            {
                Map m = new Map();
                m.Add("agent_id", pick.Creator.ID);
                m.Add("pick_id", pick.PickID);
                m.Add("creator_id", pick.Creator.ID);
                m.Add("top_pick", pick.TopPick.ToString());
                m.Add("name", pick.Name);
                m.Add("desc", pick.Description);
                m.Add("snapshot_id", pick.SnapshotID);
                m.Add("sort_order", pick.SortOrder.ToString());
                m.Add("enabled", pick.Enabled.ToString());
                m.Add("sim_name", pick.SimName);
                m.Add("parcel_uuid", pick.ParcelID);
                m.Add("parcel_name", pick.ParcelName);
                m.Add("pos_global", pick.GlobalPosition);
                OpenSimXmlRpcCall("picks_update", m);
            }

            public void Delete(UUID id)
            {
                Map m = new Map();
                m.Add("pick_id", id);
                OpenSimXmlRpcCall("picks_delete", m);
            }
        }

        public class OpenSimNotesConnector : OpenSimProfileConnector, INotesInterface
        {
            public OpenSimNotesConnector(ProfileConnector connector, string uri)
                : base(connector, uri)
            {

            }

            public string this[UUI user, UUI target]
            {
                get
                {
                    Map map = new Map();
                    map.Add("avatar_id", user.ID);
                    map.Add("uuid", target.ID);
                    Map res = (Map)(((AnArray)OpenSimXmlRpcCall("avatarnotesrequest", map))[0]);
                    return res["notes"].ToString();
                }
                set
                {
                    Map map = new Map();
                    map.Add("avatar_id", user.ID);
                    map.Add("target_id", target.ID);
                    map.Add("notes", value);
                    OpenSimXmlRpcCall("avatar_notes_update", map);
                }
            }
        }

        public class OpenSimUserPreferencesConnector : OpenSimProfileConnector, IUserPreferencesInterface
        {
            public OpenSimUserPreferencesConnector(ProfileConnector connector, string uri)
                : base(connector, uri)
            {

            }

            public ProfilePreferences this[UUI user]
            {
                get
                {
                    Map map = new Map();
                    map.Add("avatar_id", user.ID);
                    Map res = (Map)(((AnArray)OpenSimXmlRpcCall("user_preferences_request", map))[0]);
                    ProfilePreferences prefs = new ProfilePreferences();
                    prefs.User = user;
                    prefs.IMviaEmail = Convert.ToBoolean(res["imviaemail"].ToString());
                    prefs.Visible = Convert.ToBoolean(res["visible"].ToString());
                    return prefs;
                }
                set
                {
                    Map m = new Map();
                    m.Add("avatar_id", user.ID);
                    m.Add("imViaEmail", value.IMviaEmail.ToString());
                    m.Add("visible", value.Visible.ToString());
                    OpenSimXmlRpcCall("user_preferences_update", m);
                }
            }
        }

        public class OpenSimPropertiesConnector : OpenSimProfileConnector, IPropertiesInterface
        {
            public OpenSimPropertiesConnector(ProfileConnector connector, string uri)
                : base(connector, uri)
            {

            }

            public ProfileProperties this[UUI user]
            {
                get
                {
                    Map map = new Map();
                    map.Add("avatar_id", user.ID);
                    Map res = (Map)(((AnArray)OpenSimXmlRpcCall("avatar_properties_request", map))[0]);
                    ProfileProperties props = new ProfileProperties();
                    props.User = user;
                    props.Partner = UUI.Unknown;
                    props.Partner.ID = res["Partner"].AsUUID;
                    props.WebUrl = res["ProfileUrl"].ToString();
                    props.WantToMask = res["wantmask"].AsUInt;
                    props.WantToText = res["wanttext"].ToString();
                    props.SkillsMask = res["skillsmask"].AsUInt;
                    props.SkillsText = res["skillstext"].ToString();
                    props.Language = res["languages"].ToString();
                    props.ImageID = res["Image"].AsUUID;
                    props.AboutText = res["AboutText"].ToString();
                    props.FirstLifeImageID = res["FirstLifeImage"].AsUUID;
                    props.FirstLifeText = res["FirstLifeAboutText"].ToString();
                    return props;
                }
            }

            public ProfileProperties this[UUI user, PropertiesUpdateFlags flags]
            {
                set
                {
                    if ((flags & PropertiesUpdateFlags.Interests) != 0)
                    {
                        Map m = new Map();
                        m.Add("avatar_id", user.ID);
                        m.Add("wantmask", ((uint)value.WantToMask).ToString());
                        m.Add("wanttext", value.WantToText);
                        m.Add("skillsmask", ((uint)value.SkillsMask).ToString());
                        m.Add("skillstext", value.SkillsText);
                        m.Add("languages", value.Language);

                        OpenSimXmlRpcCall("avatar_interests_update", m);
                    }

                    if((flags & PropertiesUpdateFlags.Properties) != 0)
                    {
                        Map m = new Map();
                        m.Add("avatar_id", user.ID);
                        m.Add("ProfileUrl", value.WebUrl);
                        m.Add("Image", value.ImageID);
                        m.Add("AboutText", value.AboutText);
                        m.Add("FirstLifeImage", value.FirstLifeImageID);
                        m.Add("FirstLifeText", value.FirstLifeText);
                        OpenSimXmlRpcCall("avatar_properties_update", m);
                    }
                }
            }
        }
    }
}
