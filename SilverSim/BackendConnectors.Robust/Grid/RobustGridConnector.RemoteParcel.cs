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
using SilverSim.ServiceInterfaces.Parcel;
using SilverSim.Types;
using SilverSim.Types.Grid;
using SilverSim.Types.Parcel;
using SilverSim.Types.StructuredData.XmlRpc;

namespace SilverSim.BackendConnectors.Robust.Grid
{
    public partial class RobustGridConnector : IRemoteParcelServiceInterface
    {
        bool IRemoteParcelServiceInterface.TryGetRequestRemoteParcel(string remoteurl, ParcelID parcelid, out ParcelMetaInfo parcelInfo)
        {
            Map structparam = new Map();
            structparam.Add("region_handle", parcelid.Location.RegionHandle.ToString());
            structparam.Add("x", parcelid.RegionPosX.ToString());
            structparam.Add("y", parcelid.RegionPosY.ToString());

            var req = new XmlRpc.XmlRpcRequest()
            {
                MethodName = "land_data"
            };
            req.Params.Add(structparam);
            XmlRpc.XmlRpcResponse res;
            try
            {
                res = RPC.DoXmlRpcRequest(remoteurl, req, TimeoutMs);
            }
            catch(XmlRpc.XmlRpcFaultException)
            {
                parcelInfo = default(ParcelMetaInfo);
                return false;
            }
            var p = res.ReturnValue as Map;
            if (p == null)
            {
                parcelInfo = default(ParcelMetaInfo);
                return false;
            }

            if(!p.ContainsKey("GlobalID"))
            {
                parcelInfo = default(ParcelMetaInfo);
                return false;
            }

            parcelInfo = new ParcelMetaInfo()
            {
                AABBMax = p["AABBMax"].AsVector3,
                AABBMin = p["AABBMin"].AsVector3,
                Area = p["Area"].AsInt,
                AuctionID = p["AuctionID"].AsUInt,
                Description = p["Description"].ToString(),
                Flags = (ParcelFlags)p["Flags"].AsInt,
                ID = p["GlobalID"].AsUUID,
                Name = p["Name"].ToString(),
                Owner = new UUI(p["OwnerID"].AsUUID),
                SalePrice = p["SalePrice"].AsInt,
                SnapshotID = p["SnapshotID"].AsUUID,
                LandingPosition = p["UserLocation"].AsVector3
            };

            IValue regAccess;

            if (p.TryGetValue("RegionAccess", out regAccess))
            {
                parcelInfo.Access = (RegionAccess)regAccess.AsInt;
            }

            p.TryGetValue("Dwell", out parcelInfo.Dwell);

            return true;
        }

        public override IRemoteParcelServiceInterface RemoteParcelService => this;
    }
}
