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
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.XmlRpc;
using System.Net;

namespace SilverSim.BackendConnectors.Robust.Gatekeeper
{
    public class RobustForeignGridConnector : ForeignGridConnector
    {
        private readonly string m_GatekeeperUrl;
        public int TimeoutMs { get; set; }

        public RobustForeignGridConnector(string gkurl)
        {
            TimeoutMs = 20000;
            m_GatekeeperUrl = gkurl;
        }

        public override RegionInfo this[string name]
        {
            get
            {
                RegionInfo info;
                string message;
                if (!TryGetValue(name, out info, out message))
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        throw new GridRegionNotFoundException(message);
                    }
                    else
                    {
                        throw new GridRegionNotFoundException();
                    }
                }

                return info;
            }
        }

        public override bool TryGetValue(string name, out RegionInfo rInfo, out string message)
        {
            UUID regionid;
            rInfo = default(RegionInfo);
            message = default(string);
            string externalname;
            if(TryLinkRegion(name, out regionid, out externalname) && TryGetRegion(regionid, out rInfo, out message))
            {
                rInfo.Name = externalname;
                return true;
            }
            return false;
        }

        private bool TryLinkRegion(string name, out UUID id, out string external_name)
        {
            id = UUID.Zero;
            external_name = default(string);
            var p = new Map();
            p.Add("region_name", name ?? string.Empty );
            var req = new XmlRpc.XmlRpcRequest("link_region");
            req.Params.Add(p);
            XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_GatekeeperUrl, req, TimeoutMs);
            if (res.ReturnValue is Map)
            {
                var d = (Map)res.ReturnValue;
                if (!d["result"].AsBoolean)
                {
                    return false;
                }

                id = d["uuid"].AsUUID;
                external_name = d["external_name"].ToString();
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryGetRegion(UUID id, out RegionInfo rInfo, out string message)
        {
            rInfo = default(RegionInfo);
            message = default(string);
            var p = new Map
            {
                ["region_uuid"] = id
            };
            var req = new XmlRpc.XmlRpcRequest("get_region");
            req.Params.Add(p);
            XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_GatekeeperUrl, req, TimeoutMs);
            if (res.ReturnValue is Map)
            {
                var d = (Map)res.ReturnValue;
                if (!d["result"].AsBoolean)
                {
                    return false;
                }
                rInfo = new RegionInfo
                {
                    ID = d["uuid"].AsUUID,
                    Location = new GridVector
                    {
                        X = d["x"].AsUInt,
                        Y = d["y"].AsUInt
                    },
                    Size = new GridVector
                    {
                        X = d.ContainsKey("size_x") ? d["size_x"].AsUInt : 256,
                        Y = d.ContainsKey("size_y") ? d["size_y"].AsUInt : 256
                    },
                    Name = d["region_name"].ToString(),
                    GridURI = m_GatekeeperUrl,
                    ServerHttpPort = d["http_port"].AsUInt,
                    ServerPort = d["internal_port"].AsUInt,
                    ServerURI = d["server_uri"].ToString(),
                    ServerIP = d["hostname"].ToString(),
                    ProtocolVariant = "OpenSim",
                };
                if (d.ContainsKey("hostname"))
                {
                    IPAddress[] address = DnsNameCache.GetHostAddresses(d["hostname"].ToString());
                    if (d.ContainsKey("internal_port") && address.Length > 0)
                    {
                        rInfo.SimIP = new IPEndPoint(address[0], (int)rInfo.ServerPort);
                    }
                }
                if (d.ContainsKey("server_uri"))
                {
                    rInfo.ServerURI = d["server_uri"].ToString();
                }
                else if (d.ContainsKey("hostname") && d.ContainsKey("http_port"))
                {
                    rInfo.ServerURI = string.Format("http://{0}:{1}/", d["hostname"], rInfo.ServerHttpPort);
                }

                if (d.ContainsKey("message"))
                {
                    message = d["message"].ToString();
                }
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
