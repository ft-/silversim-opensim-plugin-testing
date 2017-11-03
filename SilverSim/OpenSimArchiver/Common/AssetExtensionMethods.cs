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

using SilverSim.Main.Common.Tar;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System.IO;

namespace SilverSim.OpenSimArchiver.Common
{
    public static class AssetExtensionMethods
    {
        public static AssetData LoadAsset(
            this TarArchiveReader reader,
            TarArchiveReader.Header hdr,
            UUI creator)
        {
            var asset = new AssetData
            {
                ID = UUID.Parse(hdr.FileName.Substring(7, 36)),
                FileName = hdr.FileName,
                Name = "From Archive",
                Data = new byte[hdr.Length]
            };

            if (hdr.Length != reader.Read(asset.Data, 0, hdr.Length))
            {
                throw new IOException();
            }

            return asset;
        }
    }
}
