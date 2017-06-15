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
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.OpenSim.Profile
{
    public partial class ProfileConnector
    {
        public class RobustProfileConnector : IProfileConnectorImplementation
        {
            private readonly string m_Uri;
            private readonly ProfileConnector m_Connector;

            public RobustProfileConnector(ProfileConnector connector, string uri)
            {
                m_Connector = connector;
                m_Uri = uri;
            }

            Dictionary<UUID, string> IClassifiedsInterface.GetClassifieds(UUI user)
            {
                var data = new Dictionary<UUID, string>();
                var m = new Map
                {
                    ["creatorId"] = user.ID
                };
                foreach(IValue iv in (AnArray)RPC.DoJson20RpcRequest(m_Uri, "avatarclassifiedsrequest", (string)UUID.Random, m, m_Connector.TimeoutMs))
                {
                    var c = (Map)iv;
                    data[c["classifieduuid"].AsUUID] = c["name"].ToString();
                }
                return data;
            }

            bool IClassifiedsInterface.TryGetValue(UUI user, UUID id, out ProfileClassified classified)
            {
                try
                {
                    var m = new Map
                    {
                        ["ClassifiedId"] = id
                    };
                    var reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "classifieds_info_query", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    classified = new ProfileClassified()
                    {
                        ClassifiedID = id,
                        Creator = new UUI(reslist["CreatorId"].AsUUID),
                        CreationDate = Date.UnixTimeToDateTime(reslist["CreationDate"].AsULong),
                        ExpirationDate = Date.UnixTimeToDateTime(reslist["ExpirationDate"].AsULong),
                        Category = reslist["Category"].AsInt,
                        Name = reslist["Name"].ToString(),
                        Description = reslist["Description"].ToString(),
                        ParcelID = new ParcelID(reslist["ParcelId"].AsUUID.GetBytes(), 0),
                        ParentEstate = reslist["ParentEstate"].AsInt,
                        SnapshotID = reslist["SnapshotId"].AsUUID,
                        SimName = reslist["SimName"].ToString(),
                        GlobalPos = reslist["GlobalPos"].AsVector3,
                        ParcelName = reslist["ParcelName"].ToString(),
                        Flags = (byte)reslist["Flags"].AsUInt,
                        Price = reslist["Price"].AsInt
                    };
                    return true;
                }
                catch
                {
                    classified = default(ProfileClassified);
                    return false;
                }
            }

            bool IClassifiedsInterface.ContainsKey(UUI user, UUID id)
            {
                try
                {
                    var m = new Map
                    {
                        ["ClassifiedId"] = id
                    };
                    RPC.DoJson20RpcRequest(m_Uri, "classifieds_info_query", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            ProfileClassified IClassifiedsInterface.this[UUI user, UUID id]
            {
                get
                {
                    ProfileClassified classified;
                    if (!((IClassifiedsInterface)this).TryGetValue(user, id, out classified))
                    {
                        throw new KeyNotFoundException();
                    }
                    return classified;
                }
            }

            void IClassifiedsInterface.Update(ProfileClassified classified)
            {
                var m = new Map
                {
                    { "ClassifiedId", classified.ClassifiedID },
                    { "CreatorId", classified.Creator.ID },
                    { "CreationDate", classified.CreationDate.AsInt },
                    { "ExpirationDate", classified.ExpirationDate.AsInt },
                    { "Category", classified.Category },
                    { "Name", classified.Name },
                    { "Description", classified.Description },
                    { "ParcelId", new UUID(classified.ParcelID.GetBytes(), 0) },
                    { "ParentEstate", classified.ParentEstate },
                    { "SnapshotId", classified.SnapshotID },
                    { "SimName", classified.SimName },
                    { "GlobalPos", classified.GlobalPos },
                    { "ParcelName", classified.ParcelName },
                    { "Flags", (int)classified.Flags },
                    { "Price", classified.Price }
                };
                RPC.DoJson20RpcRequest(m_Uri, "classified_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
            }

            void IClassifiedsInterface.Delete(UUID id)
            {
                var m = new Map
                {
                    ["ClassifiedId"] = id
                };
                RPC.DoJson20RpcRequest(m_Uri, "classified_delete", (string)UUID.Random, m, m_Connector.TimeoutMs);
            }

            Dictionary<UUID, string> IPicksInterface.GetPicks(UUI user)
            {
                var data = new Dictionary<UUID, string>();
                var m = new Map
                {
                    ["creatorId"] = user.ID
                };
                foreach (IValue iv in (AnArray)RPC.DoJson20RpcRequest(m_Uri, "avatarpicksrequest", (string)UUID.Random, m, m_Connector.TimeoutMs))
                {
                    var c = (Map)iv;
                    data[c["pickuuid"].AsUUID] = c["name"].ToString();
                }
                return data;
            }

            bool IPicksInterface.TryGetValue(UUI user, UUID id, out ProfilePick pick)
            {
                try
                {
                    var m = new Map
                    {
                        ["ClassifiedId"] = id,
                        ["CreatorId"] = user.ID
                    };
                    var reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "pickinforequest", (string)UUID.Random, m, m_Connector.TimeoutMs);

                    pick = new ProfilePick()
                    {
                        PickID = reslist["PickId"].AsUUID,
                        Creator = user,
                        TopPick = (ABoolean)reslist["TopPick"],
                        Name = reslist["Name"].ToString(),
                        OriginalName = reslist["OriginalName"].ToString(),
                        Description = reslist["Desc"].ToString(),
                        ParcelID = new ParcelID(reslist["ParcelId"].AsUUID.GetBytes(), 0),
                        SnapshotID = reslist["SnapshotId"].AsUUID,
                        ParcelName = reslist["ParcelName"].ToString(),
                        SimName = reslist["SimName"].ToString(),
                        GlobalPosition = reslist["GlobalPos"].AsVector3,
                        SortOrder = reslist["SortOrder"].AsInt,
                        Enabled = (ABoolean)reslist["Enabled"]
                    };
                    if (reslist.ContainsKey("Gatekeeper"))
                    {
                        pick.GatekeeperURI = reslist["Gatekeeper"].ToString();
                    }
                    return true;
                }
                catch
                {
                    pick = default(ProfilePick);
                    return false;
                }
            }

            bool IPicksInterface.ContainsKey(UUI user, UUID id)
            {
                try
                {
                    var m = new Map
                    {
                        ["ClassifiedId"] = id,
                        ["CreatorId"] = user.ID
                    };
                    RPC.DoJson20RpcRequest(m_Uri, "pickinforequest", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            ProfilePick IPicksInterface.this[UUI user, UUID id]
            {
                get
                {
                    var m = new Map
                    {
                        ["ClassifiedId"] = id,
                        ["CreatorId"] = user.ID
                    };
                    var reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "pickinforequest", (string)UUID.Random, m, m_Connector.TimeoutMs);

                    var pick = new ProfilePick()
                    {
                        PickID = reslist["PickId"].AsUUID,
                        Creator = user,
                        TopPick = (ABoolean)reslist["TopPick"],
                        Name = reslist["Name"].ToString(),
                        OriginalName = reslist["OriginalName"].ToString(),
                        Description = reslist["Desc"].ToString(),
                        ParcelID = new ParcelID(reslist["ParcelId"].AsUUID.GetBytes(), 0),
                        SnapshotID = reslist["SnapshotId"].AsUUID,
                        ParcelName = reslist["ParcelName"].ToString(),
                        SimName = reslist["SimName"].ToString(),
                        GlobalPosition = reslist["GlobalPos"].AsVector3,
                        SortOrder = reslist["SortOrder"].AsInt,
                        Enabled = (ABoolean)reslist["Enabled"]
                    };
                    if (reslist.ContainsKey("Gatekeeper"))
                    {
                        pick.GatekeeperURI = reslist["Gatekeeper"].ToString();
                    }
                    return pick;
                }
            }

            void IPicksInterface.Update(ProfilePick pick)
            {
                var m = new Map
                {
                    { "PickId", pick.PickID },
                    { "CreatorId", pick.Creator.ID },
                    { "TopPick", pick.TopPick },
                    { "Name", pick.Name },
                    { "Desc", pick.Description },
                    { "ParcelId", new UUID(pick.ParcelID.GetBytes(), 0) },
                    { "SnapshotId", pick.SnapshotID },
                    { "ParcelName", pick.ParcelName },
                    { "SimName", pick.SimName },
                    { "Gatekeeper", pick.GatekeeperURI },
                    { "GlobalPos", pick.GlobalPosition.ToString() },
                    { "SortOrder", pick.SortOrder },
                    { "Enabled", pick.Enabled }
                };
                RPC.DoJson20RpcRequest(m_Uri, "picks_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
            }

            void IPicksInterface.Delete(UUID id)
            {
                var m = new Map
                {
                    ["pickId"] = id
                };
                RPC.DoJson20RpcRequest(m_Uri, "picks_delete", (string)UUID.Random, m, m_Connector.TimeoutMs);
            }

            bool INotesInterface.TryGetValue(UUI user, UUI target, out string notes)
            {
                try
                {
                    var m = new Map
                    {
                        ["UserId"] = user.ID,
                        ["TargetId"] = target.ID
                    };
                    var reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "avatarnotesrequest", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    notes = reslist["Notes"].ToString();
                    return true;
                }
                catch
                {
                    notes = string.Empty;
                    return false;
                }
            }

            bool INotesInterface.ContainsKey(UUI user, UUI target)
            {
                try
                {
                    var m = new Map
                    {
                        ["UserId"] = user.ID,
                        ["TargetId"] = target.ID
                    };
                    RPC.DoJson20RpcRequest(m_Uri, "avatarnotesrequest", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            string INotesInterface.this[UUI user, UUI target]
            {
                get
                {
                    var m = new Map
                    {
                        ["UserId"] = user.ID,
                        ["TargetId"] = target.ID
                    };
                    var reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "avatarnotesrequest", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    return reslist["Notes"].ToString();
                }
                set
                {
                    var m = new Map
                    {
                        { "UserId", user.ID },
                        { "TargetId", target.ID },
                        { "Notes", value }
                    };
                    RPC.DoJson20RpcRequest(m_Uri, "avatar_notes_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
                }
            }

            bool IUserPreferencesInterface.TryGetValue(UUI user, out ProfilePreferences prefs)
            {
                try
                {
                    var m = new Map
                    {
                        ["UserId"] = user.ID
                    };
                    var reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "user_preferences_request", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    prefs = new ProfilePreferences()
                    {
                        User = user,
                        IMviaEmail = (ABoolean)reslist["IMViaEmail"],
                        Visible = (ABoolean)reslist["Visible"]
                    };
                    return true;
                }
                catch
                {
                    prefs = default(ProfilePreferences);
                    return false;
                }
            }

            bool IUserPreferencesInterface.ContainsKey(UUI user)
            {
                try
                {
                    var m = new Map
                    {
                        ["UserId"] = user.ID
                    };
                    RPC.DoJson20RpcRequest(m_Uri, "user_preferences_request", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            ProfilePreferences IUserPreferencesInterface.this[UUI user]
            {
                get
                {
                    var m = new Map
                    {
                        ["UserId"] = user.ID
                    };
                    Map reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "user_preferences_request", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    ProfilePreferences prefs = new ProfilePreferences()
                    {
                        User = user,
                        IMviaEmail = (ABoolean)reslist["IMViaEmail"],
                        Visible = (ABoolean)reslist["Visible"]
                    };
                    return prefs;
                }
                set
                {
                    var m = new Map
                    {
                        { "UserId", user.ID },
                        { "IMViaEmail", value.IMviaEmail },
                        { "Visible", value.Visible }
                    };
                    RPC.DoJson20RpcRequest(m_Uri, "user_preferences_update", (string)UUID.Random, m, m_Connector.TimeoutMs);
                }
            }

            ProfileProperties IPropertiesInterface.this[UUI user]
            {
                get
                {
                    var m = new Map
                    {
                        ["UserId"] = user.ID
                    };
                    var reslist = (Map)RPC.DoJson20RpcRequest(m_Uri, "avatar_properties_request", (string)UUID.Random, m, m_Connector.TimeoutMs);
                    return new ProfileProperties()
                    {
                        User = user,
                        Partner = new UUI(reslist["PartnerId"].AsUUID),
                        PublishProfile = (ABoolean)reslist["PublishProfile"],
                        PublishMature = (ABoolean)reslist["PublishMature"],
                        WebUrl = reslist["WebUrl"].ToString(),
                        WantToMask = reslist["WantToMask"].AsUInt,
                        WantToText = reslist["WantToText"].ToString(),
                        SkillsMask = reslist["SkillsMask"].AsUInt,
                        SkillsText = reslist["SkillsText"].ToString(),
                        Language = reslist["Language"].ToString(),
                        ImageID = reslist["ImageId"].AsUUID,
                        AboutText = reslist["AboutText"].ToString(),
                        FirstLifeImageID = reslist["FirstLifeImageId"].AsUUID,
                        FirstLifeText = reslist["FirstLifeText"].ToString()
                    };
                }
            }

            ProfileProperties IPropertiesInterface.this[UUI user, PropertiesUpdateFlags flags]
            {
                set
                {
                    var m = new Map
                    {
                        { "UserId", user.ID },
                        { "PartnerId", value.Partner.ID },
                        { "PublishProfile", value.PublishProfile },
                        { "PublishMature", value.PublishMature },
                        { "WebUrl", value.WebUrl },
                        { "WantToMask", (int)value.WantToMask },
                        { "WantToText", value.WantToText },
                        { "SkillsMask", (int)value.SkillsMask },
                        { "SkillsText", value.SkillsText },
                        { "Language", value.Language },
                        { "ImageId", value.ImageID },
                        { "AboutText", value.AboutText },
                        { "FirstLifeImageId", value.FirstLifeImageID },
                        { "FirstLifeText", value.FirstLifeText }
                    };
                    if ((flags & PropertiesUpdateFlags.Interests) != 0)
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
