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
            private readonly string m_Uri;
            private readonly ProfileConnector m_Connector;

            public OpenSimProfileConnector(ProfileConnector connector, string uri)
            {
                m_Uri = uri;
                m_Connector = connector;
            }

            protected IValue OpenSimXmlRpcCall(string methodName, Map structparam)
            {
                var req = new XmlRpc.XmlRpcRequest()
                {
                    MethodName = methodName
                };
                req.Params.Add(structparam);
                XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, m_Connector.TimeoutMs);
                var p = res.ReturnValue as Map;
                if (p == null)
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
                var req = new XmlRpc.XmlRpcRequest()
                {
                    MethodName = methodName
                };
                req.Params.Add(structparam);
                XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, m_Connector.TimeoutMs);
                var p = res.ReturnValue as Map;
                if (p == null)
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
                var req = new XmlRpc.XmlRpcRequest()
                {
                    MethodName = methodName
                };
                req.Params.Add(structparam);
                XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, m_Connector.TimeoutMs);
                var p = res.ReturnValue as Map;
                if (p == null)
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
                var map = new Map
                {
                    ["uuid"] = user.ID
                };
                var res = (AnArray)OpenSimXmlRpcCall("avatarclassifiedsrequest", map);
                var classifieds = new Dictionary<UUID, string>();
                foreach(IValue iv in res)
                {
                    var m = (Map)iv;
                    classifieds.Add(m["classifiedid"].AsUUID, m["name"].ToString());
                }
                return classifieds;
            }

            bool IClassifiedsInterface.TryGetValue(UUI user, UUID id, out ProfileClassified classified)
            {
                IValue iv;
                var map = new Map
                {
                    ["classifiedID"] = user.ID
                };
                if (!TryOpenSimXmlRpcCall("classifieds_info_query", map, out iv))
                {
                    classified = default(ProfileClassified);
                    return false;
                }
                var res = (Map)(((AnArray)iv)[0]);

                classified = new ProfileClassified()
                {
                    ClassifiedID = res["classifieduuid"].AsUUID,
                    Creator = new UUI(res["creatoruuid"].AsUUID),
                    CreationDate = Date.UnixTimeToDateTime(res["creationdate"].AsULong),
                    ExpirationDate = Date.UnixTimeToDateTime(res["expirationdate"].AsULong),
                    Category = res["category"].AsInt,
                    Name = res["name"].ToString(),
                    Description = res["description"].ToString(),
                    ParcelID = new ParcelID(res["parceluuid"].AsUUID.GetBytes(), 0),
                    ParentEstate = res["parentestate"].AsInt,
                    SnapshotID = res["snapshotuuid"].AsUUID,
                    SimName = res["simname"].ToString(),
                    GlobalPos = res["posglobal"].AsVector3,
                    ParcelName = res["parcelname"].ToString(),
                    Flags = (byte)res["classifiedflags"].AsUInt,
                    Price = res["priceforlisting"].AsInt
                };
                return true;
            }

            bool IClassifiedsInterface.ContainsKey(UUI user, UUID id)
            {
                var map = new Map
                {
                    ["classifiedID"] = user.ID
                };
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
                var map = new Map
                {
                    { "parcelname", classified.ParcelName },
                    { "creatorUUID", classified.Creator.ID },
                    { "classifiedUUID", classified.ClassifiedID },
                    { "category", ((int)classified.Category).ToString() },
                    { "name", classified.Name },
                    { "description", classified.Description },
                    { "parentestate", classified.ParentEstate.ToString() },
                    { "snapshotUUID", classified.SnapshotID },
                    { "sim_name", classified.SimName },
                    { "globalpos", classified.GlobalPos.ToString() },
                    { "classifiedFlags", ((uint)classified.Flags).ToString() },
                    { "classifiedPrice", classified.Price.ToString() },
                    { "parcelUUID", classified.ParcelID.ToString() },
                    { "pos_global", classified.GlobalPos.ToString() }
                };
                OpenSimXmlRpcCall("classified_update", map);
            }

            void IClassifiedsInterface.Delete(UUID id)
            {
                var map = new Map
                {
                    ["classifiedID"] = id
                };
                OpenSimXmlRpcCall("classified_delete", map);
            }

            Dictionary<UUID, string> IPicksInterface.GetPicks(UUI user)
            {
                var map = new Map
                {
                    ["uuid"] = user.ID
                };
                var res = (AnArray)OpenSimXmlRpcCall("avatarpicksrequest", map);
                var classifieds = new Dictionary<UUID, string>();
                foreach (IValue iv in res)
                {
                    var m = (Map)iv;
                    classifieds.Add(m["pickid"].AsUUID, m["name"].ToString());
                }
                return classifieds;
            }

            private ProfilePick ConvertToProfilePick(Map res) => new ProfilePick()
            {
                PickID = res["pickuuid"].AsUUID,
                Creator = new UUI(res["creatoruuid"].AsUUID),
                TopPick = Convert.ToBoolean(res["toppick"].ToString()),
                ParcelID = new ParcelID(res["parceluuid"].AsUUID.GetBytes(), 0),
                Name = res["name"].ToString(),
                Description = res["description"].ToString(),
                SnapshotID = res["snapshotuuid"].AsUUID,
                OriginalName = res["originalname"].ToString(),
                SimName = res["simname"].ToString(),
                GlobalPosition = res["posglobal"].AsVector3,
                SortOrder = res["sortorder"].AsInt,
                Enabled = Convert.ToBoolean(res["enabled"].ToString())
            };

            bool IPicksInterface.TryGetValue(UUI user, UUID id, out ProfilePick pick)
            {
                var map = new Map
                {
                    ["avatar_id"] = user.ID,
                    ["pick_id"] = id
                };
                IValue iv;
                if(!TryOpenSimXmlRpcCall("pickinforequest", map, out iv))
                {
                    pick = default(ProfilePick);
                    return false;
                }
                var res = (Map)(((AnArray)iv)[0]);
                pick = ConvertToProfilePick(res);
                return true;
            }

            bool IPicksInterface.ContainsKey(UUI user, UUID id)
            {
                var map = new Map
                {
                    ["avatar_id"] = user.ID,
                    ["pick_id"] = id
                };
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
                    var map = new Map
                    {
                        ["avatar_id"] = user.ID,
                        ["pick_id"] = id
                    };
                    var res = (Map)(((AnArray)OpenSimXmlRpcCall("pickinforequest", map))[0]);
                    return ConvertToProfilePick(res);
                }
            }

            void IPicksInterface.Update(ProfilePick pick)
            {
                var m = new Map
                {
                    { "agent_id", pick.Creator.ID },
                    { "pick_id", pick.PickID },
                    { "creator_id", pick.Creator.ID },
                    { "top_pick", pick.TopPick.ToString() },
                    { "name", pick.Name },
                    { "desc", pick.Description },
                    { "snapshot_id", pick.SnapshotID },
                    { "sort_order", pick.SortOrder.ToString() },
                    { "enabled", pick.Enabled.ToString() },
                    { "sim_name", pick.SimName },
                    { "parcel_uuid", new UUID(pick.ParcelID.GetBytes(), 0) },
                    { "parcel_name", pick.ParcelName },
                    { "pos_global", pick.GlobalPosition }
                };
                OpenSimXmlRpcCall("picks_update", m);
            }

            void IPicksInterface.Delete(UUID id)
            {
                var m = new Map
                {
                    ["pick_id"] = id
                };
                OpenSimXmlRpcCall("picks_delete", m);
            }

            bool INotesInterface.TryGetValue(UUI user, UUI target, out string notes)
            {
                var map = new Map
                {
                    ["avatar_id"] = user.ID,
                    ["uuid"] = target.ID
                };
                IValue iv;
                if(!TryOpenSimXmlRpcCall("avatarnotesrequest", map, out iv))
                {
                    notes = string.Empty;
                    return false;
                }
                var res = (Map)(((AnArray)iv)[0]);
                notes = res["notes"].ToString();
                return true;
            }

            bool INotesInterface.ContainsKey(UUI user, UUI target)
            {
                var map = new Map
                {
                    ["avatar_id"] = user.ID,
                    ["uuid"] = target.ID
                };
                return TryOpenSimXmlRpcCall("avatarnotesrequest", map);
            }

            string INotesInterface.this[UUI user, UUI target]
            {
                get
                {
                    var map = new Map
                    {
                        ["avatar_id"] = user.ID,
                        ["uuid"] = target.ID
                    };
                    var res = (Map)(((AnArray)OpenSimXmlRpcCall("avatarnotesrequest", map))[0]);
                    return res["notes"].ToString();
                }
                set
                {
                    var map = new Map
                    {
                        { "avatar_id", user.ID },
                        { "target_id", target.ID },
                        { "notes", value }
                    };
                    OpenSimXmlRpcCall("avatar_notes_update", map);
                }
            }

            bool IUserPreferencesInterface.TryGetValue(UUI user, out ProfilePreferences prefs)
            {
                var map = new Map
                {
                    ["avatar_id"] = user.ID
                };
                IValue iv;
                if(!TryOpenSimXmlRpcCall("user_preferences_request", map, out iv))
                {
                    prefs = default(ProfilePreferences);
                    return false;
                }
                var res = (Map)(((AnArray)iv)[0]);
                prefs = new ProfilePreferences()
                {
                    User = user,
                    IMviaEmail = Convert.ToBoolean(res["imviaemail"].ToString()),
                    Visible = Convert.ToBoolean(res["visible"].ToString())
                };
                return true;
            }

            bool IUserPreferencesInterface.ContainsKey(UUI user)
            {
                var map = new Map
                {
                    ["avatar_id"] = user.ID
                };
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
                    var map = new Map
                    {
                        ["avatar_id"] = user.ID
                    };
                    var res = (Map)(((AnArray)OpenSimXmlRpcCall("user_preferences_request", map))[0]);
                    return new ProfilePreferences()
                    {
                        User = user,
                        IMviaEmail = Convert.ToBoolean(res["imviaemail"].ToString()),
                        Visible = Convert.ToBoolean(res["visible"].ToString())
                    };
                }
                set
                {
                    var m = new Map
                    {
                        { "avatar_id", user.ID },
                        { "imViaEmail", value.IMviaEmail.ToString() },
                        { "visible", value.Visible.ToString() }
                    };
                    OpenSimXmlRpcCall("user_preferences_update", m);
                }
            }

            ProfileProperties IPropertiesInterface.this[UUI user]
            {
                get
                {
                    var map = new Map
                    {
                        ["avatar_id"] = user.ID
                    };
                    var res = (Map)(((AnArray)OpenSimXmlRpcCall("avatar_properties_request", map))[0]);
                    return new ProfileProperties()
                    {
                        User = user,
                        Partner = new UUI(res["Partner"].AsUUID),
                        WebUrl = res["ProfileUrl"].ToString(),
                        WantToMask = res["wantmask"].AsUInt,
                        WantToText = res["wanttext"].ToString(),
                        SkillsMask = res["skillsmask"].AsUInt,
                        SkillsText = res["skillstext"].ToString(),
                        Language = res["languages"].ToString(),
                        ImageID = res["Image"].AsUUID,
                        AboutText = res["AboutText"].ToString(),
                        FirstLifeImageID = res["FirstLifeImage"].AsUUID,
                        FirstLifeText = res["FirstLifeAboutText"].ToString()
                    };
                }
            }

            ProfileProperties IPropertiesInterface.this[UUI user, PropertiesUpdateFlags flags]
            {
                set
                {
                    if ((flags & PropertiesUpdateFlags.Interests) != 0)
                    {
                        var m = new Map
                        {
                            { "avatar_id", user.ID },
                            { "wantmask", ((uint)value.WantToMask).ToString() },
                            { "wanttext", value.WantToText },
                            { "skillsmask", ((uint)value.SkillsMask).ToString() },
                            { "skillstext", value.SkillsText },
                            { "languages", value.Language }
                        };
                        OpenSimXmlRpcCall("avatar_interests_update", m);
                    }

                    if((flags & PropertiesUpdateFlags.Properties) != 0)
                    {
                        var m = new Map
                        {
                            { "avatar_id", user.ID },
                            { "ProfileUrl", value.WebUrl },
                            { "Image", value.ImageID },
                            { "AboutText", value.AboutText },
                            { "FirstLifeImage", value.FirstLifeImageID },
                            { "FirstLifeText", value.FirstLifeText }
                        };
                        OpenSimXmlRpcCall("avatar_properties_update", m);
                    }
                }
            }
        }
    }
}
