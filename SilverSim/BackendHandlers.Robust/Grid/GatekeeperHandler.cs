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
using SilverSim.ServiceInterfaces.Grid;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SilverSim.Types.StructuredData.XmlRpc;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Types;
using SilverSim.Types.Grid;
using SilverSim.Threading;
using SilverSim.ServiceInterfaces.ServerParam;

namespace SilverSim.BackendHandlers.Robust.Grid
{
    [Description("Hypergrid Gatekeeper Server")]
    [ServerParam("AllowTeleportsToAnyRegion", Description = "Controls whether teleports anywhere is allowed", ParameterType = typeof(bool), Type = ServerParamType.GlobalOnly, DefaultValue = true)]
    [ServerParamStartsWith("HGRegionRedirect_")]
    public class GatekeeperHandler : IPlugin, IServerParamListener, IServerParamAnyListener
    {
        readonly string m_GridServiceName;
        GridServiceInterface m_GridService;
        bool m_AllowTeleportsToAnyRegion = true;
        readonly RwLockedDictionary<UUID, UUID> m_RegionRedirects = new RwLockedDictionary<UUID, UUID>();
        readonly RwLockedDictionary<UUID, string> m_RegionRedirectMessages = new RwLockedDictionary<UUID, string>();

        public GatekeeperHandler(IConfig ownSection)
        {
            m_GridServiceName = ownSection.GetString("GridService", "GridService");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_GridService = loader.GetService<GridServiceInterface>(m_GridServiceName);
            HttpXmlRpcHandler xmlRpcServer = loader.XmlRpcServer;
            xmlRpcServer.XmlRpcMethods.Add("link_region", LinkRegion);
            xmlRpcServer.XmlRpcMethods.Add("get_region", GetRegion);
        }

        XmlRpc.XmlRpcResponse LinkRegion(XmlRpc.XmlRpcRequest req)
        {
            Map reqdata;
            Map resdata = new Map();
            IValue iv;
            resdata.Add("result", "False");
            RegionInfo ri;
            if(!(req.Params.Count == 1 && req.Params.TryGetValue(0, out reqdata) &&
                reqdata.TryGetValue("region_name", out iv) &&
                m_GridService.TryGetValue(UUID.Zero, iv.ToString(), out ri)))
            {
                List<RegionInfo> ris = m_GridService.GetDefaultHypergridRegions(UUID.Zero);
                if(ris.Count != 0)
                {
                    ri = ris[0];
                }
                else
                {
                    ri = null;
                }
            }

            if (null != ri)
            {
                resdata["result"] = new AString("True");
                resdata.Add("uuid", ri.ID);
                resdata.Add("handle", ri.Location.RegionHandle.ToString());
                resdata.Add("size_x", ri.Size.X.ToString());
                resdata.Add("size_y", ri.Size.Y.ToString());
                //resdata.Add("region_image", );
                resdata.Add("external_name", ri.ServerURI);
            }

            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        XmlRpc.XmlRpcResponse GetRegion(XmlRpc.XmlRpcRequest req)
        {
            Map reqdata;
            Map resdata = new Map();
            IValue iv;
            UUID regionid;
            RegionInfo ri;
            resdata.Add("result", "false");
            if (req.Params.Count == 1 && req.Params.TryGetValue(0, out reqdata) &&
                reqdata.TryGetValue("region_uuid", out iv) &&
                UUID.TryParse(iv.ToString(), out regionid)
                )
            {
                UUID redirectid;
                string message = string.Empty;
                if(m_RegionRedirects.TryGetValue(regionid, out redirectid) &&
                    m_GridService.ContainsKey(redirectid))
                {
                    regionid = redirectid;
                    if(!m_RegionRedirectMessages.TryGetValue(regionid, out message))
                    {
                        message = string.Empty;
                    }
                }
                if (m_GridService.TryGetValue(UUID.Zero, regionid, out ri))
                {
                    if (!m_AllowTeleportsToAnyRegion && !ri.Flags.HasFlag(RegionFlags.DefaultHGRegion))
                    {
                        List<RegionInfo> ris = m_GridService.GetDefaultHypergridRegions(UUID.Zero);
                        if (ris.Count != 0)
                        {
                            ri = ris[0];
                            resdata.Add("message", "Teleporting you to the default region");
                        }
                        else
                        {
                            ri = null;
                        }
                    }

                    if (null != ri)
                    {
                        resdata["result"] = new AString("true");
                        if(!string.IsNullOrEmpty(message))
                        {
                            resdata.Add("message", message);
                        }
                        resdata.Add("uuid", ri.ID.ToString());
                        resdata.Add("x", ri.Location.X.ToString());
                        resdata.Add("y", ri.Location.Y.ToString());
                        resdata.Add("size_x", ri.Size.X.ToString());
                        resdata.Add("size_y", ri.Size.Y.ToString());
                        resdata.Add("region_name", ri.Name);
                        resdata.Add("hostname", ri.ServerIP.ToString());
                        resdata.Add("http_port", ri.ServerHttpPort.ToString());
                        resdata.Add("internal_port", ri.ServerPort.ToString());
                        resdata.Add("server_uri", ri.ServerURI);
                    }
                }
            }

            return new XmlRpc.XmlRpcResponse { ReturnValue = resdata };
        }

        [ServerParam("AllowTeleportsToAnyRegion")]
        public void AllowTeleportsToAnyRegionUpdated(UUID regionID, string value)
        {
            if(regionID != UUID.Zero)
            {
                return;
            }

            bool b;
            m_AllowTeleportsToAnyRegion = (value.Length == 0) || (bool.TryParse(value, out b) && b);
        }

        public IReadOnlyDictionary<string, ServerParamAttribute> ServerParams
        {
            get
            {
                return new Dictionary<string, ServerParamAttribute>();
            }
        }

        public void TriggerParameterUpdated(UUID regionID, string parametername, string value)
        {
            if(parametername.StartsWith("HGRegionRedirect_") && UUID.Zero != regionID)
            {
                UUID redirectid;
                if(string.IsNullOrEmpty(value))
                {
                    m_RegionRedirects.Remove(regionID);
                }
                else if(UUID.TryParse(value, out redirectid))
                {
                    m_RegionRedirects[regionID] = redirectid;
                }
            }
            if (parametername.StartsWith("HGRegionRedirectMessage_") && UUID.Zero != regionID)
            {
                if (string.IsNullOrEmpty(value))
                {
                    m_RegionRedirectMessages.Remove(regionID);
                }
                else
                {
                    m_RegionRedirectMessages[regionID] = value;
                }
            }
        }
    }

    [PluginName("GatekeeperHandler")]
    public class GatekeeperHandlerFactory : IPluginFactory
    {
        public GatekeeperHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new GatekeeperHandler(ownSection);
        }
    }
}
