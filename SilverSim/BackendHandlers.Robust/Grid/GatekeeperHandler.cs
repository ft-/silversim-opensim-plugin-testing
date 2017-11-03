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
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.ServerParam;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.XmlRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SilverSim.BackendHandlers.Robust.Grid
{
    [Description("Hypergrid Gatekeeper Server")]
    [PluginName("GatekeeperHandler")]
    [ServerParam("Gatekeeper.AllowDirectTeleportViaHg", DefaultValue = true, ParameterType = typeof(bool))]
    [ServerParam("Gatekeeper.DenyMessageWhenHgTeleport", DefaultValue = "Teleporting to the default region.", ParameterType = typeof(string))]
    [ServerParam("Gatekeeper.RedirectMessageWhenHgTeleport", ParameterType = typeof(string))]
    [ServerParam("Gatekeeper.RedirectToOtherRegion", ParameterType = typeof(UUID))]
    public class GatekeeperHandler : IPlugin, IServerParamListener
    {
        private HttpXmlRpcHandler m_XmlRpcServer;

        private readonly string m_GridServiceName;
        private GridServiceInterface m_GridService;
        private readonly UUID m_ScopeID;
        private RwLockedDictionary<UUID, bool> m_AllowDirectTeleportViaHgMap = new RwLockedDictionary<UUID, bool>();
        private RwLockedDictionary<UUID, string> m_DenyMessages = new RwLockedDictionary<UUID, string>();
        private RwLockedDictionary<UUID, string> m_RedirectMessages = new RwLockedDictionary<UUID, string>();
        private RwLockedDictionary<UUID, UUID> m_RedirectToOtherRegion = new RwLockedDictionary<UUID, UUID>();

        [ServerParam("Gatekeeper.RedirectToOtherRegion")]
        public void RedirectToOtherRegionUpdated(UUID regionID, string value)
        {
            UUID id;
            if (string.IsNullOrEmpty(value) || !UUID.TryParse(value, out id))
            {
                m_RedirectToOtherRegion.Remove(regionID);
            }
            else
            {
                m_RedirectToOtherRegion[regionID] = id;
            }
        }

        private bool IsRedirectedToOtherRegion(UUID regionID, out UUID redirectID)
        {
            return (m_RedirectToOtherRegion.TryGetValue(regionID, out redirectID) || m_RedirectToOtherRegion.TryGetValue(UUID.Zero, out redirectID)) && redirectID != UUID.Zero;
        }

        [ServerParam("Gatekeeper.DenyMessageWhenHgTeleport")]
        public void DenyMessageWhenHgTeleportUpdated(UUID regionID, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                m_DenyMessages.Remove(regionID);
            }
            else
            {
                m_DenyMessages[regionID] = value;
            }
        }

        private string GetDenyMessage(UUID regionId)
        {
            string denymsg;
            if (m_DenyMessages.TryGetValue(regionId, out denymsg))
            {
                return denymsg;
            }
            else if (m_DenyMessages.TryGetValue(UUID.Zero, out denymsg))
            {
                return denymsg;
            }
            else
            {
                return "Teleporting to the default region.";
            }
        }

        [ServerParam("Gatekeeper.RedirectMessageWhenHgTeleport")]
        public void RedirectMessageWhenHgTeleportUpdated(UUID regionID, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                m_RedirectMessages.Remove(regionID);
            }
            else
            {
                m_RedirectMessages[regionID] = value;
            }
        }

        private string GetRedirectMessage(UUID regionId)
        {
            string redirectmsg;
            if (m_RedirectMessages.TryGetValue(regionId, out redirectmsg))
            {
                return redirectmsg;
            }
            else if (m_RedirectMessages.TryGetValue(UUID.Zero, out redirectmsg))
            {
                return redirectmsg;
            }
            else
            {
                return string.Empty;
            }
        }

        [ServerParam("Gatekeeper.AllowDirectTeleportViaHg")]
        public void AllowDirectTeleportViaHgUpdated(UUID regionID, string value)
        {
            bool boolval;
            if (string.IsNullOrEmpty(value))
            {
                m_AllowDirectTeleportViaHgMap.Remove(regionID);
            }
            else
            {
                m_AllowDirectTeleportViaHgMap[regionID] = bool.TryParse(value, out boolval) && boolval;
            }
        }

        private bool IsDirectTeleportViaHgAllowed(UUID regionID)
        {
            bool bval;
            return (m_AllowDirectTeleportViaHgMap.TryGetValue(regionID, out bval) ||
                m_AllowDirectTeleportViaHgMap.TryGetValue(UUID.Zero, out bval)) && bval;
        }

        public GatekeeperHandler(IConfig ownConfig)
        {
            m_GridServiceName = ownConfig.GetString("GridService", "GridService");
            m_ScopeID = ownConfig.GetString("ScopeID", UUID.Zero.ToString());
        }

        public void Shutdown()
        {
            m_XmlRpcServer.XmlRpcMethods.Remove("link_region");
            m_XmlRpcServer.XmlRpcMethods.Remove("get_region");
            m_XmlRpcServer = null;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_GridService = loader.GetService<GridServiceInterface>(m_GridServiceName);
            m_XmlRpcServer = loader.XmlRpcServer;
            m_XmlRpcServer.XmlRpcMethods.Add("link_region", LinkRegion);
            m_XmlRpcServer.XmlRpcMethods.Add("get_region", GetRegion);
        }

        private XmlRpc.XmlRpcResponse LinkRegion(XmlRpc.XmlRpcRequest req)
        {
            string region_name = string.Empty;
            try
            {
                region_name = (((Map)req.Params[0])["region_name"]).ToString();
            }
            catch
            {
                region_name = string.Empty;
            }

            Map resdata = new Map();
            bool success = false;
            if (string.IsNullOrEmpty(region_name))
            {
                List<RegionInfo> regions = m_GridService.GetDefaultHypergridRegions(m_ScopeID);
                foreach (RegionInfo rInfo in regions)
                {
                    if ((rInfo.Flags & RegionFlags.RegionOnline) == 0)
                    {
                        continue;
                    }

                    resdata.Add("uuid", rInfo.ID);
                    resdata.Add("handle", rInfo.Location.RegionHandle.ToString());
                    resdata.Add("region_image", string.Empty);
                    resdata.Add("external_name", rInfo.ServerURI + " " + rInfo.Name);
                    success = true;
                    break;
                }
            }
            else
            {
                RegionInfo rInfo;
                if (m_GridService.TryGetValue(m_ScopeID, region_name, out rInfo))
                {
                    resdata.Add("uuid", rInfo.ID);
                    resdata.Add("handle", rInfo.Location.RegionHandle.ToString());
                    resdata.Add("region_image", string.Empty);
                    resdata.Add("external_name", rInfo.ServerURI + " " + rInfo.Name);
                    success = true;
                }
            }

            resdata.Add("result", success);

            return new XmlRpc.XmlRpcResponse
            {
                ReturnValue = resdata
            };
        }

        private XmlRpc.XmlRpcResponse GetRegion(XmlRpc.XmlRpcRequest req)
        {
            UUID region_uuid = (((Map)req.Params[0])["region_uuid"]).AsUUID;
            var resdata = new Map();
            try
            {
                RegionInfo rInfo;
                if (m_GridService.TryGetValue(region_uuid, out rInfo) && (rInfo.Flags & RegionFlags.RegionOnline) != 0)
                {
                    UUID redirect_id;
                    if (!IsDirectTeleportViaHgAllowed(region_uuid))
                    {
                        resdata.Add("message", GetDenyMessage(region_uuid));
                        rInfo = null;
                        foreach (RegionInfo defInfo in m_GridService.GetDefaultHypergridRegions(m_ScopeID))
                        {
                            if ((defInfo.Flags & RegionFlags.RegionOnline) != 0)
                            {
                                continue;
                            }
                            rInfo = defInfo;
                            break;
                        }
                    }
                    else if (IsRedirectedToOtherRegion(region_uuid, out redirect_id))
                    {
                        string redirectmsg = GetRedirectMessage(region_uuid);
                        if (!string.IsNullOrEmpty(redirectmsg))
                        {
                            resdata.Add("message", redirectmsg);
                        }
                        if (!m_GridService.TryGetValue(redirect_id, out rInfo))
                        {
                            rInfo = null;
                        }
                    }
                }

                if (rInfo != null)
                {
                    resdata.Add("uuid", rInfo.ID);
                    resdata.Add("handle", rInfo.Location.RegionHandle.ToString());
                    resdata.Add("x", rInfo.Location.X.ToString());
                    resdata.Add("y", rInfo.Location.Y.ToString());
                    resdata.Add("size_x", rInfo.Size.X.ToString());
                    resdata.Add("size_y", rInfo.Size.Y.ToString());
                    resdata.Add("region_name", rInfo.Name);
                    Uri serverURI = new Uri(rInfo.ServerURI);
                    resdata.Add("hostname", serverURI.Host);
                    resdata.Add("http_port", rInfo.ServerHttpPort.ToString());
                    resdata.Add("internal_port", rInfo.ServerPort.ToString());
                    resdata.Add("server_uri", rInfo.ServerURI);
                    resdata.Add("result", true);
                }
                else
                {
                    resdata["message"] = new AString("No destination region found.");
                    resdata.Add("result", false);
                }
            }
            catch
            {
                resdata.Add("result", false);
            }
            return new XmlRpc.XmlRpcResponse
            {
                ReturnValue = resdata
            };
        }
    }
}
