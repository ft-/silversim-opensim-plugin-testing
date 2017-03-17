﻿// SilverSim is distributed under the terms of the
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

using SilverSim.Main.Common.Rpc;
using SilverSim.Types;
using SilverSim.Types.Profile;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.OpenSim.Profile
{
    public partial class ProfileConnector
    {
        public class OpenSimProfileConnector : IProfileConnectorImplementation
        {
            readonly string m_Uri;
            readonly ProfileConnector m_Connector;

            public OpenSimProfileConnector(ProfileConnector connector, string uri)
            {
                m_Uri = uri;
                m_Connector = connector;
            }

            protected IValue OpenSimXmlRpcCall(string methodName, Map structparam)
            {
                XmlRpc.XmlRpcRequest req = new XmlRpc.XmlRpcRequest();
                req.MethodName = methodName;
                req.Params.Add(structparam);
                XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, m_Connector.TimeoutMs);
                Map p = res.ReturnValue as Map;
                if (null == p)
                {
                    throw new InvalidDataException("Unexpected OpenSimProfile return value");
                }
                if (!p.ContainsKey("success"))
                {
                    throw new InvalidDataException("Unexpected OpenSimProfile return value");
                }

                if (p["success"].ToString().ToLower() != "true")
                {
                    throw new KeyNotFoundException();
                }
                return (p.ContainsKey("data")) ? p["data"] : null; /* some calls have no data */
            }

            protected bool TryOpenSimXmlRpcCall(string methodName, Map structparam, out IValue iv)
            {
                XmlRpc.XmlRpcRequest req = new XmlRpc.XmlRpcRequest();
                req.MethodName = methodName;
                req.Params.Add(structparam);
                XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, m_Connector.TimeoutMs);
                Map p = res.ReturnValue as Map;
                if (null == p)
                {
                    throw new InvalidDataException("Unexpected OpenSimProfile return value");
                }
                if (!p.ContainsKey("success"))
                {
                    throw new InvalidDataException("Unexpected OpenSimProfile return value");
                }

                if (p["success"].ToString().ToLower() != "true")
                {
                    iv = default(IValue);
                    return false;
                }
                iv = (p.ContainsKey("data")) ? p["data"] : null; /* some calls have no data */
                return true;
            }

            protected bool TryOpenSimXmlRpcCall(string methodName, Map structparam)
            {
                XmlRpc.XmlRpcRequest req = new XmlRpc.XmlRpcRequest();
                req.MethodName = methodName;
                req.Params.Add(structparam);
                XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, m_Connector.TimeoutMs);
                Map p = res.ReturnValue as Map;
                if (null == p)
                {
                    throw new InvalidDataException("Unexpected OpenSimProfile return value");
                }
                if (!p.ContainsKey("success"))
                {
                    throw new InvalidDataException("Unexpected OpenSimProfile return value");
                }

                if (p["success"].ToString().ToLower() != "true")
                {
                    return false;
                }
                return true;
            }

            Dictionary<UUID, string> IClassifiedsInterface.GetClassifieds(UUI user)
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

            bool IClassifiedsInterface.TryGetValue(UUI user, UUID id, out ProfileClassified classified)
            {
                IValue iv;
                Map map = new Map();
                map.Add("classifiedID", user.ID);
                if(!TryOpenSimXmlRpcCall("classifieds_info_query", map, out iv))
                {
                    classified = default(ProfileClassified);
                    return false;
                }
                Map res = (Map)(((AnArray)iv)[0]);

                classified = new ProfileClassified();
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
                return true;
            }

            bool IClassifiedsInterface.ContainsKey(UUI user, UUID id)
            {
                Map map = new Map();
                map.Add("classifiedID", user.ID);
                return TryOpenSimXmlRpcCall("classifieds_info_query", map);
            }

            ProfileClassified IClassifiedsInterface.this[UUI user, UUID id]
            {
                get
                {
                    ProfileClassified classified;
                    if(!((IClassifiedsInterface)this).TryGetValue(user, id, out classified))
                    {
                        throw new KeyNotFoundException();
                    }
                    return classified;
                }
            }


            void IClassifiedsInterface.Update(ProfileClassified classified)
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

            void IClassifiedsInterface.Delete(UUID id)
            {
                Map map = new Map();
                map["classifiedID"] = id;
                OpenSimXmlRpcCall("classified_delete", map);
            }

            Dictionary<UUID, string> IPicksInterface.GetPicks(UUI user)
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

            ProfilePick ConvertToProfilePick(Map res)
            {
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

            bool IPicksInterface.TryGetValue(UUI user, UUID id, out ProfilePick pick)
            {
                Map map = new Map();
                map.Add("avatar_id", user.ID);
                map.Add("pick_id", id);
                IValue iv;
                if(!TryOpenSimXmlRpcCall("pickinforequest", map, out iv))
                {
                    pick = default(ProfilePick);
                    return false;
                }
                Map res = (Map)(((AnArray)iv)[0]);
                pick = ConvertToProfilePick(res);
                return true;
            }

            bool IPicksInterface.ContainsKey(UUI user, UUID id)
            {
                Map map = new Map();
                map.Add("avatar_id", user.ID);
                map.Add("pick_id", id);
                IValue iv;
                if (!TryOpenSimXmlRpcCall("pickinforequest", map, out iv))
                {
                    return false;
                }
                return ((AnArray)iv)[0] is Map;
            }

            ProfilePick IPicksInterface.this[UUI user, UUID id]
            {
                get 
                {
                    Map map = new Map();
                    map.Add("avatar_id", user.ID);
                    map.Add("pick_id", id);
                    Map res = (Map)(((AnArray)OpenSimXmlRpcCall("pickinforequest", map))[0]);
                    ProfilePick pick = ConvertToProfilePick(res);
                    return pick;
                }
            }


            void IPicksInterface.Update(ProfilePick pick)
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

            void IPicksInterface.Delete(UUID id)
            {
                Map m = new Map();
                m.Add("pick_id", id);
                OpenSimXmlRpcCall("picks_delete", m);
            }

            bool INotesInterface.TryGetValue(UUI user, UUI target, out string notes)
            {
                Map map = new Map();
                map.Add("avatar_id", user.ID);
                map.Add("uuid", target.ID);
                IValue iv;
                if(!TryOpenSimXmlRpcCall("avatarnotesrequest", map, out iv))
                {
                    notes = string.Empty;
                    return false;
                }
                Map res = (Map)(((AnArray)iv)[0]);
                notes = res["notes"].ToString();
                return true;
            }

            bool INotesInterface.ContainsKey(UUI user, UUI target)
            {
                Map map = new Map();
                map.Add("avatar_id", user.ID);
                map.Add("uuid", target.ID);
                return TryOpenSimXmlRpcCall("avatarnotesrequest", map);
            }

            string INotesInterface.this[UUI user, UUI target]
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

            bool IUserPreferencesInterface.TryGetValue(UUI user, out ProfilePreferences prefs)
            {
                Map map = new Map();
                map.Add("avatar_id", user.ID);
                IValue iv;
                if(!TryOpenSimXmlRpcCall("user_preferences_request", map, out iv))
                {
                    prefs = default(ProfilePreferences);
                    return false;
                }
                Map res = (Map)(((AnArray)iv)[0]);
                prefs = new ProfilePreferences();
                prefs.User = user;
                prefs.IMviaEmail = Convert.ToBoolean(res["imviaemail"].ToString());
                prefs.Visible = Convert.ToBoolean(res["visible"].ToString());
                return true;
            }

            bool IUserPreferencesInterface.ContainsKey(UUI user)
            {
                Map map = new Map();
                map.Add("avatar_id", user.ID);
                IValue iv;
                if (!TryOpenSimXmlRpcCall("user_preferences_request", map, out iv))
                {
                    return false;
                }
                return true;
            }

            ProfilePreferences IUserPreferencesInterface.this[UUI user]
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

            ProfileProperties IPropertiesInterface.this[UUI user]
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

            ProfileProperties IPropertiesInterface.this[UUI user, PropertiesUpdateFlags flags]
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
