// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.CmdIO;
using SilverSim.Main.Common.Tar;
using SilverSim.Scene.Types.Object;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Parcel;
using SilverSim.Viewer.Messages.LayerData;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace SilverSim.OpenSimArchiver.RegionArchiver
{
    public static partial class OAR
    {
        [Flags]
        [SuppressMessage("Gendarme.Rules.Design", "FlagsShouldNotDefineAZeroValueRule")]
        public enum SaveOptions
        {
            None = 0,
            NoAssets = 0x00000001,
            Publish = 0x00000002,
        }

        public static void Save(
            SceneInterface scene,
            SaveOptions options,
            Stream outputFile,
            TTY console_io = null)
        {
            using (GZipStream gzip = new GZipStream(outputFile, CompressionMode.Compress))
            {
                TarArchiveWriter writer = new TarArchiveWriter(gzip);

                bool saveAssets = (options & SaveOptions.NoAssets) == 0;
                XmlSerializationOptions xmloptions = XmlSerializationOptions.None;
                if ((options & SaveOptions.Publish) == 0)
                {
                    xmloptions |= XmlSerializationOptions.WriteOwnerInfo;
                }

                writer.WriteFile("archive.xml", WriteArchiveXml08(scene, saveAssets));

                Dictionary<string, AssetData> objectAssets = new Dictionary<string, AssetData>();

                foreach (ObjectGroup sog in scene.Objects)
                {
                    if (sog.IsTemporary || sog.IsAttached)
                    {
                        /* skip temporary or attached */
                        continue;
                    }
                    AssetData data = sog.Asset(xmloptions | XmlSerializationOptions.WriteXml2);
                    objectAssets.Add(sog.Name + "_" + sog.GlobalPosition.X_String + "-" + sog.GlobalPosition.Y_String + "-" + sog.GlobalPosition.Z_String + "__" + sog.ID.ToString() + ".xml", data);
                }

                if (saveAssets)
                {
                    /* we only parse sim details when saving assets */
                    List<UUID> assetIDs = new List<UUID>();
                    AssetData data;

                    foreach (AssetData objdata in objectAssets.Values)
                    {
                        foreach (UUID id in objdata.References)
                        {
                            if (id != UUID.Zero && !assetIDs.Contains(id))
                            {
                                assetIDs.Add(id);
                            }
                        }
                    }

                    foreach (ParcelInfo pinfo in scene.Parcels)
                    {
                        if (pinfo.MediaID != UUID.Zero)
                        {
                            assetIDs.Add(pinfo.MediaID);
                        }
                        if (pinfo.SnapshotID != UUID.Zero)
                        {
                            assetIDs.Add(pinfo.SnapshotID);
                        }
                    }

                    int assetidx = 0;
                    while (assetidx < assetIDs.Count)
                    {
                        UUID assetID = assetIDs[assetidx++];
                        try
                        {
                            data = scene.AssetService[assetID];
                        }
                        catch
                        {
                            continue;
                        }
                        writer.WriteAsset(data);
                        try
                        {
                            foreach (UUID refid in data.References)
                            {
                                if (!assetIDs.Contains(refid))
                                {
                                    assetIDs.Add(refid);
                                }
                            }
                        }
                        catch
#if DEBUG
                            (Exception e)
#endif
                        {
                            console_io.WriteFormatted("Failed to parse asset {0}", assetID);
                        }
                    }
                }

                foreach (ParcelInfo pinfo in scene.Parcels)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (XmlTextWriter xmlwriter = ms.UTF8XmlTextWriter())
                        {
                            xmlwriter.WriteStartElement("LandData");
                            {
                                xmlwriter.WriteNamedValue("Area", pinfo.Area);
                                xmlwriter.WriteNamedValue("AuctionID", pinfo.AuctionID);
                                xmlwriter.WriteNamedValue("AuthBuyerID", pinfo.AuthBuyer.ID);
                                xmlwriter.WriteNamedValue("Category", (byte)pinfo.Category);
                                xmlwriter.WriteNamedValue("ClaimDate", pinfo.ClaimDate.DateTimeToUnixTime().ToString());
                                xmlwriter.WriteNamedValue("ClaimPrice", pinfo.ClaimPrice);
                                xmlwriter.WriteNamedValue("GlobalID", pinfo.ID);
                                if ((options & SaveOptions.Publish) != 0)
                                {
                                    xmlwriter.WriteNamedValue("GroupID", UUID.Zero);
                                    xmlwriter.WriteNamedValue("IsGroupOwned", false);
                                }
                                else
                                {
                                    xmlwriter.WriteNamedValue("GroupID", pinfo.Group.ID);
                                    xmlwriter.WriteNamedValue("IsGroupOwned", pinfo.GroupOwned);
                                }
                                xmlwriter.WriteNamedValue("Bitmap", Convert.ToBase64String(pinfo.LandBitmap.Data));
                                xmlwriter.WriteNamedValue("Description", pinfo.Description);
                                xmlwriter.WriteNamedValue("Flags", (uint)pinfo.Flags);
                                xmlwriter.WriteNamedValue("LandingType", (uint)pinfo.LandingType);
                                xmlwriter.WriteNamedValue("Name", pinfo.Name);
                                xmlwriter.WriteNamedValue("Status", (uint)pinfo.Status);
                                xmlwriter.WriteNamedValue("LocalID", pinfo.LocalID);
                                xmlwriter.WriteNamedValue("MediaAutoScale", pinfo.MediaAutoScale);
                                xmlwriter.WriteNamedValue("MediaID", pinfo.MediaID);
                                if (pinfo.MediaURI != null)
                                {
                                    xmlwriter.WriteNamedValue("MediaURL", pinfo.MediaURI.ToString());
                                }
                                else
                                {
                                    xmlwriter.WriteStartElement("MediaURL");
                                    xmlwriter.WriteEndElement();
                                }
                                if (pinfo.MusicURI != null)
                                {
                                    xmlwriter.WriteNamedValue("MusicURL", pinfo.MusicURI.ToString());
                                }
                                else
                                {
                                    xmlwriter.WriteStartElement("MusicURL");
                                    xmlwriter.WriteEndElement();
                                }
                                xmlwriter.WriteNamedValue("OwnerID", pinfo.Owner.ID);
                                xmlwriter.WriteStartElement("ParcelAccessList");
                                {
#warning Implement saving Parcel Access List
                                }
                                xmlwriter.WriteEndElement();
                                xmlwriter.WriteNamedValue("PassHours", pinfo.PassHours);
                                xmlwriter.WriteNamedValue("PassPrice", pinfo.PassPrice);
                                xmlwriter.WriteNamedValue("SalePrice", pinfo.SalePrice);
                                xmlwriter.WriteNamedValue("SnapshotID", pinfo.SnapshotID);
                                xmlwriter.WriteNamedValue("UserLocation", pinfo.LandingPosition.ToString());
                                xmlwriter.WriteNamedValue("UserLookAt", pinfo.LandingLookAt.ToString());
                                xmlwriter.WriteNamedValue("Dwell", pinfo.Dwell);
                                xmlwriter.WriteNamedValue("OtherCleanTime", pinfo.OtherCleanTime);
                            }
                            xmlwriter.WriteEndElement();
                            xmlwriter.Flush();

                            writer.WriteFile("landdata/" + pinfo.ID.ToString() + ".xml", ms.GetBuffer());
                        }
                    }
                }

                foreach (KeyValuePair<string, AssetData> kvp in objectAssets)
                {
                    writer.WriteFile("objects/" + kvp.Key, kvp.Value.Data);
                }

                writer.WriteFile("terrains/" + scene.Name + ".r32", GenTerrainFile(scene.Terrain.AllPatches));
                writer.WriteEndOfTar();
            }
        }

        static byte[] WriteArchiveXml08(SceneInterface scene, bool assetsIncluded)
        {
            using(MemoryStream ms = new MemoryStream())
            {
                using(XmlTextWriter writer = ms.UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("archive");
                    writer.WriteAttributeString("major_version", "0");
                    writer.WriteAttributeString("major_version", "8");
                    {
                        writer.WriteStartElement("creation_info");
                        {
                            writer.WriteNamedValue("datetime", Date.GetUnixTime().ToString());
                            writer.WriteNamedValue("id", scene.ID);
                        }
                        writer.WriteEndElement();

                        writer.WriteNamedValue("assets_included", assetsIncluded);

                        writer.WriteStartElement("region_info");
                        {
                            writer.WriteNamedValue("is_megaregion", false);
                            writer.WriteNamedValue("size_in_meters", 
                                string.Format("{0},{1}", scene.SizeX, scene.SizeY));
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.Flush();

                    return ms.GetBuffer();
                }
            }
        }

        static byte[] GenTerrainFile(List<LayerPatch> terrain)
        {
            using (MemoryStream output = new MemoryStream())
            {
                float[] outdata = new float[terrain.Count * LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES * LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES];
                uint minX = terrain[0].X;
                uint minY = terrain[0].Y;
                uint maxX = terrain[0].X;
                uint maxY = terrain[0].Y;

                /* determine line width */
                foreach (LayerPatch p in terrain)
                {
                    minX = Math.Min(minX, p.X);
                    minY = Math.Min(minY, p.Y);
                    maxX = Math.Max(maxX, p.X);
                    maxY = Math.Max(maxY, p.Y);
                }

                uint linewidth = maxX - minX + 1;

                /* build output data */
                foreach (LayerPatch p in terrain)
                {
                    for (uint y = 0; y < LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES; ++y)
                    {
                        for (uint x = 0; x < LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES; ++x)
                        {
                            outdata[LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES * y + x + p.XYToYNormal(linewidth, minY)] = p[x, y];
                        }
                    }
                }

                using (BinaryWriter bs = new BinaryWriter(output))
                {
                    foreach (float f in outdata)
                    {
                        bs.Write(f);
                    }
                    bs.Flush();
                    return output.GetBuffer();
                }
            }
        }

        public static uint XYToYNormal(this LayerPatch p, uint lineWidth, uint minY)
        {
            return (p.Y - minY) * lineWidth + p.X;
        }
    }
}
