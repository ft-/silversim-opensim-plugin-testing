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

using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types;
using SilverSim.Types.Parcel;
using System.ComponentModel;
using static SilverSim.Types.StructuredData.XmlRpc.XmlRpc;

namespace SilverSim.BackendHandlers.Robust.Simulation
{
    [Description("Remote Parcel Handler")]
    [PluginName("SimParcelHandler")]
    public sealed class RemoteParcelHandler : IPlugin, IPluginShutdown
    {
        private HttpXmlRpcHandler m_XmlRpcServer;
        private SceneList m_Scenes;

        public ShutdownOrder ShutdownOrder => ShutdownOrder.Any;

        public void Shutdown()
        {
            m_XmlRpcServer.XmlRpcMethods.Remove("land_data");
            m_XmlRpcServer = null;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Scenes = loader.Scenes;
            m_XmlRpcServer = loader.XmlRpcServer;
            m_XmlRpcServer.XmlRpcMethods.Add("land_data", HandleRemoteParcel);
        }

        XmlRpcResponse HandleRemoteParcel(XmlRpcRequest req)
        {
            Map m;
            if(req.Params.Count != 1)
            {
                throw new XmlRpcFaultException(4, "Invalid parameters");
            }
            m = req.Params[0] as Map;
            if(m == null)
            {
                throw new XmlRpcFaultException(4, "Invalid parameters");
            }

            IValue iv_regionHandle;
            IValue iv_x;
            IValue iv_y;
            if(!m.TryGetValue("region_handle", out iv_regionHandle) ||
                !m.TryGetValue("x", out iv_x) ||
                !m.TryGetValue("y", out iv_y))
            {
                throw new XmlRpcFaultException(4, "Invalid parameters");
            }

            ParcelID parcelId = new ParcelID();
            try
            {
                parcelId.Location.RegionHandle = iv_regionHandle.AsULong;
                parcelId.RegionPosX = iv_x.AsUInt;
                parcelId.RegionPosY = iv_y.AsUInt;
            }
            catch
            {
                throw new XmlRpcFaultException(4, "Invalid parameters");
            }

            SceneInterface scene;
            ParcelInfo pInfo;

            Map response = new Map();
            if(m_Scenes.TryGetValue(parcelId.Location.RegionHandle, out scene) &&
                scene.Parcels.TryGetValue(parcelId.RegionPos, out pInfo))
            {
                response.Add("AABBMax", pInfo.AABBMax.ToString());
                response.Add("AABBMin", pInfo.AABBMin.ToString());
                response.Add("Area", pInfo.Area.ToString());
                response.Add("AuctionID", pInfo.AuctionID.ToString());
                response.Add("Description", pInfo.Description);
                response.Add("Flags", ((int)pInfo.Flags).ToString());
                response.Add("GlobalID", new UUID(parcelId.GetBytes(), 0).ToString());
                response.Add("Name", pInfo.Name);
                response.Add("OwnerID", pInfo.Owner.ID.ToString());
                response.Add("SalePrice", pInfo.SalePrice.ToString());
                response.Add("SnapshotID", pInfo.SnapshotID.ToString());
                response.Add("UserLocation", pInfo.LandingPosition.ToString());
                response.Add("RegionAccess", ((byte)scene.GetRegionInfo().Access).ToString());
                response.Add("Dwell", pInfo.Dwell.ToString());
            }

            return new XmlRpcResponse { ReturnValue = response };
        }
    }
}
