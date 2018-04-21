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

using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.Agent;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace SilverSim.BackendConnectors.Robust.StructuredData.Agent
{
    public class PostData
    {
        public DestinationInfo Destination = new DestinationInfo();
        public ClientInfo Client = new ClientInfo();
        public SessionInfo Session = new SessionInfo();
        public CircuitInfo Circuit = new CircuitInfo();
        public AppearanceInfo Appearance = new AppearanceInfo();
        public UserAccount Account = new UserAccount();

        [Serializable]
        public class InvalidAgentPostSerializationException : Exception
        {
            public InvalidAgentPostSerializationException()
            {
            }

            public InvalidAgentPostSerializationException(string message)
                : base(message)
            {
            }

            protected InvalidAgentPostSerializationException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public InvalidAgentPostSerializationException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }

        public static PostData Deserialize(Stream input)
        {
            var agentparams = new PostData();
            var parms = Json.Deserialize(input) as Map;
            if (parms == null)
            {
                throw new InvalidAgentPostSerializationException("Invalid JSON AgentPostData");
            }

            /*-----------------------------------------------------------------*/
            /* SessionInfo */
            if (parms.ContainsKey("service_session_id"))
            {
                agentparams.Session.ServiceSessionID = parms["service_session_id"].ToString();
            }
            agentparams.Session.SessionID = parms["session_id"].AsUUID;
            agentparams.Session.SecureSessionID = parms["secure_session_id"].AsUUID;

            /*-----------------------------------------------------------------*/
            /* Circuit Info */
            agentparams.Circuit.CircuitCode = parms["circuit_code"].AsUInt;
            agentparams.Circuit.CapsPath = parms["caps_path"].ToString();
            agentparams.Circuit.IsChild = parms.ContainsKey("child") && parms["child"].AsBoolean;
            if(parms.ContainsKey("children_seeds"))
            {
                foreach(IValue seedv in (AnArray)parms["children_seeds"])
                {
                    var seed = (Map)seedv;
                    UInt64 regionhandle;
                    if (!UInt64.TryParse(seed["handle"].ToString(), out regionhandle))
                    {
                        throw new InvalidAgentPostSerializationException();
                    }
                    agentparams.Circuit.ChildrenCapSeeds[regionhandle] = seed["seed"].ToString();
                }
            }

            /*-----------------------------------------------------------------*/
            /* Destination */
            agentparams.Destination = new DestinationInfo
            {
                ID = parms["destination_uuid"].AsUUID,
                Location = new GridVector { X = parms["destination_x"].AsUInt, Y = parms["destination_y"].AsUInt },
                Name = "HG Destination",
                Position = parms["start_pos"].AsVector3
            };
            if (parms.ContainsKey("teleport_flags"))
            {
                uint tpflags = parms["teleport_flags"].AsUInt;
                agentparams.Destination.TeleportFlags = (TeleportFlags)(tpflags);
            }
            else
            {
                agentparams.Destination.TeleportFlags = TeleportFlags.None;
            }
            if (parms.ContainsKey("gatekeeper_serveruri"))
            {
                agentparams.Destination.ServerURI = parms["destination_serveruri"].ToString();
                agentparams.Destination.GridURI = parms["gatekeeper_serveruri"].ToString();
                agentparams.Destination.LocalToGrid = false;
            }
            else
            {
                agentparams.Destination.GridURI = string.Empty;
                agentparams.Destination.LocalToGrid = true;
            }

            /*-----------------------------------------------------------------*/
            /* Account */
            agentparams.Account = new UserAccount
            {
                Principal = new UGUIWithName { ID = parms["agent_id"].AsUUID, FirstName = parms["first_name"].ToString(), LastName = parms["last_name"].ToString() },
                IsLocalToGrid = false,
                UserLevel = 0
            };

            /*-----------------------------------------------------------------*/
            /* Client Info */
            agentparams.Client = new ClientInfo
            {
                ClientIP = parms["client_ip"].ToString(),
                ClientVersion = parms["viewer"].ToString(),
                Channel = parms["channel"].ToString(),
                Mac = parms["mac"].ToString(),
                ID0 = parms["id0"].ToString()
            };

            /*-----------------------------------------------------------------*/
            /* Service URLs */
            if (parms.ContainsKey("serviceurls") && parms["serviceurls"] is Map)
            {
                foreach (KeyValuePair<string, IValue> kvp in (Map)(parms["serviceurls"]))
                {
                    agentparams.Account.ServiceURLs.Add(kvp.Key, kvp.Value.ToString());
                }
            }
            else if (parms.ContainsKey("service_urls") && parms["service_urls"] is AnArray)
            {
                var array = (AnArray)parms["service_urls"];
                if (array.Count % 2 != 0)
                {
                    throw new InvalidAgentPostSerializationException("Invalid service_urls block in AgentPostData");
                }
                int i;
                for (i = 0; i < array.Count; i += 2)
                {
                    agentparams.Account.ServiceURLs.Add(array[i].ToString(), array[i + 1].ToString());
                }
            }

            if (agentparams.Account.ServiceURLs.ContainsKey("GatekeeperURI") &&
                (string.IsNullOrEmpty(agentparams.Account.ServiceURLs["GatekeeperURI"]) || agentparams.Account.ServiceURLs["GatekeeperURI"] == "/"))
            {
                agentparams.Account.ServiceURLs["GatekeeperURI"] = agentparams.Account.ServiceURLs["HomeURI"];
            }

            if(!(agentparams.Account.ServiceURLs.ContainsKey("HomeURI") &&
                Uri.TryCreate(agentparams.Account.ServiceURLs["HomeURI"],
                    UriKind.Absolute, out agentparams.Account.Principal.HomeURI)))
            {
                agentparams.Account.Principal.HomeURI = null;
            }

            /*-----------------------------------------------------------------*/
            /* Appearance */
            if (parms.ContainsKey("packed_appearance"))
            {
                var appearancePack = (Map)parms["packed_appearance"];
                if (appearancePack.ContainsKey("height"))
                {
                    agentparams.Appearance.AvatarHeight = appearancePack["height"].AsReal;
                }
                if (appearancePack.ContainsKey("serial"))
                {
                    agentparams.Appearance.Serial = appearancePack["serial"].AsInt;
                }

                if(appearancePack.ContainsKey("visualparams"))
                {
                    var vParams = (AnArray)appearancePack["visualparams"];
                    var visualParams = new byte[vParams.Count];

                    int i;
                    for (i = 0; i < vParams.Count; ++i)
                    {
                        visualParams[i] = (byte)vParams[i].AsUInt;
                    }
                    agentparams.Appearance.VisualParams = visualParams;
                }

                if(appearancePack.ContainsKey("textures"))
                {
                    var texArray = (AnArray)appearancePack["textures"];
                    int i;
                    for (i = 0; i < AppearanceInfo.AvatarTextureData.TextureCount; ++i)
                    {
                        agentparams.Appearance.AvatarTextures[i] = texArray[i].AsUUID;
                    }
                }

                if (appearancePack.ContainsKey("wearables"))
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
                                AssetID = (wp.ContainsKey("asset")) ?
                                wp["asset"].AsUUID :
                                UUID.Zero
                            };
                            var type = (WearableType)i;
                            agentparams.Appearance.Wearables[type, n++] = wi;
                        }
                    }
                }

                if (appearancePack.ContainsKey("attachments"))
                {
                    foreach (IValue apv in (AnArray)appearancePack["attachments"])
                    {
                        var ap = (Map)apv;
                        uint apid;
                        if (!uint.TryParse(ap["point"].ToString(), out apid))
                        {
                            throw new InvalidAgentPostSerializationException();
                        }
                        agentparams.Appearance.Attachments[(AttachmentPoint)apid][ap["item"].AsUUID] = UUID.Zero;
                    }
                }
            }

            return agentparams;
        }

        private void WriteJSONString(TextWriter w, string name, string value)
        {
            w.Write(string.Format("\"{0}\":\"{1}\"", Json.SerializeString(name), Json.SerializeString(value)));
        }

        private void WriteJSONString(TextWriter w, string name, UUID value)
        {
            w.Write(string.Format("\"{0}\":\"{1}\"", Json.SerializeString(name), Json.SerializeString((string)value)));
        }

        private void WriteJSONValue(TextWriter w, string name, uint value)
        {
            w.Write(string.Format("\"{0}\":{1}", Json.SerializeString(name), value));
        }

        private void WriteJSONValue(TextWriter w, string name, double value)
        {
            w.Write(string.Format(CultureInfo.InvariantCulture, "\"{0}\":{1}", Json.SerializeString(name), value));
        }

        private void WriteJSONValue(TextWriter w, string name, bool value)
        {
            w.Write(string.Format("\"{0}\":{1}", Json.SerializeString(name), value ? "true" : "false"));
        }

        public void Serialize(Stream output, int maxAllowedWearables)
        {
            string prefix;
            using (TextWriter w = output.UTF8StreamWriterLeaveOpen())
            {
                w.Write("{");
                /*-----------------------------------------------------------------*/
                /* SessionInfo */
                WriteJSONString(w, "service_session_id", Session.ServiceSessionID);
                w.Write(",");
                WriteJSONString(w, "session_id", Session.SessionID);
                w.Write(",");
                WriteJSONString(w, "secure_session_id", Session.SecureSessionID);
                w.Write(",");

                /*-----------------------------------------------------------------*/
                /* Circuit Info */
                WriteJSONString(w, "circuit_code", Circuit.CircuitCode.ToString());
                w.Write(",");
                WriteJSONString(w, "caps_path", Circuit.CapsPath);
                w.Write(",");
                WriteJSONValue(w, "child", Circuit.IsChild);
                w.Write(",");
                w.Write("\"children_seeds\":[");
                prefix = string.Empty;
                foreach (KeyValuePair<UInt64, string> kvp in Circuit.ChildrenCapSeeds)
                {
                    w.Write(prefix);
                    w.Write("{");
                    WriteJSONString(w, "handle", kvp.Key.ToString());
                    w.Write(",");
                    WriteJSONString(w, "seed", kvp.Value);
                    w.Write("}");
                    prefix = ",";
                }
                w.Write("],");

                /*-----------------------------------------------------------------*/
                /* Destination */
                WriteJSONString(w, "destination_uuid", Destination.ID);
                w.Write(",");
                WriteJSONString(w, "destination_x", Destination.Location.X.ToString());
                w.Write(",");
                WriteJSONString(w, "destination_y", Destination.Location.Y.ToString());
                w.Write(",");
                WriteJSONString(w, "start_pos", Destination.Position.ToString());
                w.Write(",");
                WriteJSONString(w, "teleport_flags", ((uint)Destination.TeleportFlags).ToString());
                w.Write(",");
                if (!Destination.LocalToGrid)
                {
                    WriteJSONString(w, "destination_serveruri", Destination.ServerURI);
                    w.Write(",");
                    WriteJSONString(w, "gatekeeper_serveruri", Destination.GridURI);
                    w.Write(",");
                }

                /*-----------------------------------------------------------------*/
                /* Account */
                WriteJSONString(w, "agent_id", Account.Principal.ID);
                w.Write(",");
                WriteJSONString(w, "first_name", Account.Principal.FirstName);
                w.Write(",");
                WriteJSONString(w, "last_name", Account.Principal.LastName);
                w.Write(",");

                /*-----------------------------------------------------------------*/
                /* Client Info */
                WriteJSONString(w, "client_ip", Client.ClientIP);
                w.Write(",");
                WriteJSONString(w, "viewer", Client.ClientVersion);
                w.Write(",");
                WriteJSONString(w, "channel", Client.Channel);
                w.Write(",");
                WriteJSONString(w, "mac", Client.Mac);
                w.Write(",");
                WriteJSONString(w, "id0", Client.ID0);
                w.Write(",");

                /*-----------------------------------------------------------------*/
                /* Service URLs */
                w.Write("\"serviceurls\":{");
                prefix = string.Empty;
                if (Account.Principal.HomeURI != null)
                {
                    WriteJSONString(w, "HomeURI", Account.Principal.HomeURI.ToString());
                    prefix = ",";
                }
                foreach (KeyValuePair<string, string> kvp in Account.ServiceURLs)
                {
                    w.Write(prefix);
                    WriteJSONString(w, kvp.Key, kvp.Value);
                    prefix = ",";
                }
                w.Write("},");
                w.Write("\"service_urls\":[");
                prefix = string.Empty;
                if (Account.Principal.HomeURI != null)
                {
                    w.Write(string.Format("\"{0}\", \"{1}\"", "HomeURI", Account.Principal.HomeURI.ToString()));
                    prefix = ",";
                }
                foreach (KeyValuePair<string, string> kvp in Account.ServiceURLs)
                {
                    w.Write(prefix);
                    w.Write(string.Format("\"{0}\", \"{1}\"", Json.SerializeString(kvp.Key), Json.SerializeString(kvp.Value)));
                    prefix = ",";
                }
                w.Write("],");

                w.Write("\"packed_appearance\":{");
                WriteJSONValue(w, "height", Appearance.AvatarHeight);
                w.Write(",");
                WriteJSONValue(w, "serial", Appearance.Serial);
                w.Write(",");
                var vParams = new StringBuilder();
                foreach (byte v in Appearance.VisualParams)
                {
                    if (vParams.Length != 0)
                    {
                        vParams.Append(',');
                    }
                    vParams.Append(v.ToString());
                }
                w.Write("\"visualparams\":[");
                w.Write(vParams.ToString());
                w.Write("]");
                w.Write(",");
                w.Write("\"textures\":[");
                prefix = string.Empty;
                {
                    int i;
                    for (i = 0; i < AppearanceInfo.AvatarTextureData.TextureCount; ++i)
                    {
                        w.Write(prefix);
                        w.Write(string.Format("\"{0}\"", Appearance.AvatarTextures[i]));
                        prefix = ",";
                    }
                }
                w.Write("],\"wearables\":[");
                {
                    uint i;
                    for (i = 0; i < (uint)WearableType.NumWearables && i < (uint)maxAllowedWearables; ++i)
                    {
                        if (i != 0)
                        {
                            w.Write(",");
                        }
                        List<AgentWearables.WearableInfo> wearables = Appearance.Wearables[(WearableType)i];
                        w.Write("[");
                        prefix = string.Empty;
                        foreach (AgentWearables.WearableInfo wi in wearables)
                        {
                            w.Write(prefix);
                            w.Write("{");
                            WriteJSONString(w, "item", wi.ItemID);
                            if (wi.AssetID != UUID.Zero)
                            {
                                w.Write(",");
                                WriteJSONString(w, "asset", wi.AssetID);
                            }
                            w.Write("}");
                            prefix = ",";
                        }
                        w.Write("]");
                    }
                }
                w.Write("], \"attachments\":[");
                {
                    prefix = string.Empty;
                    foreach (KeyValuePair<AttachmentPoint, RwLockedDictionary<UUID, UUID>> kvp in Appearance.Attachments)
                    {
                        foreach (KeyValuePair<UUID, UUID> kvpInner in kvp.Value)
                        {
                            w.Write(prefix);
                            w.Write("{");
                            WriteJSONValue(w, "point", (uint)kvp.Key);
                            WriteJSONString(w, "item", kvpInner.Key);
                            if (kvpInner.Value != UUID.Zero)
                            {
                                WriteJSONString(w, "asset", kvpInner.Value);
                            }
                            w.Write("}");
                            prefix = ",";
                        }
                    }
                }
                w.Write("]");

                w.Write("}"); /* packed_appearance */
                w.Write("}");
            }
        }
    }
}
