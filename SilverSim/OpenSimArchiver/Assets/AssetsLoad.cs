// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.Tar;
using SilverSim.OpenSimArchiver.Common;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using SilverSim.Types.Asset;
using System.IO;
using System.IO.Compression;

namespace SilverSim.OpenSimArchiver.Assets
{
    public static class AssetsLoad
    {
        public static void Load(
            AssetServiceInterface assetService,
            UUI owner,
            Stream inputFile)
        {
            using (GZipStream gzipStream = new GZipStream(inputFile, CompressionMode.Decompress))
            {
                using (TarArchiveReader reader = new TarArchiveReader(gzipStream))
                {

                    for (; ; )
                    {
                        TarArchiveReader.Header header;
                        try
                        {
                            header = reader.ReadHeader();
                        }
                        catch (TarArchiveReader.EndOfTarException)
                        {
                            return;
                        }

                        if (header.FileType == TarFileType.File)
                        {
                            if (header.FileName.StartsWith("assets/"))
                            {
                                /* Load asset */
                                AssetData ad = reader.LoadAsset(header, owner);
                                try
                                {
                                    assetService.Exists(ad.ID);
                                }
                                catch
                                {
                                    assetService.Store(ad);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
