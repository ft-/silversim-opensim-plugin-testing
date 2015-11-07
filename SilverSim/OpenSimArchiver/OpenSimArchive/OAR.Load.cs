// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.Tar;
using SilverSim.OpenSimArchiver.Common;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Object;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Parcel;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;

namespace SilverSim.OpenSimArchiver.RegionArchiver
{
    public static partial class OAR
    {
        [Serializable]
        public class OARLoadingErrorException : Exception
        {
            public OARLoadingErrorException()
            {

            }

            public OARLoadingErrorException(string message)
                : base(message)
            {

            }

            public OARLoadingErrorException(string message, Exception innerException)
                : base(message, innerException)
            {

            }

            protected OARLoadingErrorException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {

            }
        }

        [Serializable]
        public class OARLoadingTriedWithoutSelectedRegionException : Exception
        {
            public OARLoadingTriedWithoutSelectedRegionException()
            {

            }

            public OARLoadingTriedWithoutSelectedRegionException(string message)
                : base(message)
            {

            }

            public OARLoadingTriedWithoutSelectedRegionException(string message, Exception innerException)
                : base(message, innerException)
            {

            }

            protected OARLoadingTriedWithoutSelectedRegionException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {

            }
        }

        [Serializable]
        public class MultiRegionOARLoadingTriedOnRegionException : Exception
        {
            public MultiRegionOARLoadingTriedOnRegionException()
            {

            }

            public MultiRegionOARLoadingTriedOnRegionException(string message)
                : base(message)
            {

            }

            public MultiRegionOARLoadingTriedOnRegionException(string message, Exception innerException)
                : base(message, innerException)
            {

            }

            protected MultiRegionOARLoadingTriedOnRegionException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {

            }
        }

        [Serializable]
        public class OARFormatException : Exception
        {
            public OARFormatException()
            {

            }

            public OARFormatException(string message)
                : base(message)
            {

            }

            public OARFormatException(string message, Exception innerException)
                : base(message, innerException)
            {

            }

            protected OARFormatException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {

            }
        }

        [Flags]
        [SuppressMessage("Gendarme.Rules.Design", "FlagsShouldNotDefineAZeroValueRule")]
        public enum LoadOptions
        {
            None = 0,
            Merge = 0x000000001,
            NoAssets = 0x00000002,
            PersistUuids = 0x00000004,
        }

        static void AddObjects(
            SceneInterface scene,
            List<ObjectGroup> sogs,
            LoadOptions options)
        {
            foreach (ObjectGroup sog in sogs)
            {
                if ((options & LoadOptions.PersistUuids) == LoadOptions.PersistUuids)
                {
                    foreach (ObjectPart part in sog.ValuesByKey1)
                    {
                        UUID oldID;
                        ObjectPart check;
                        if(scene.Primitives.TryGetValue(part.ID, out check))
                        {
                            oldID = part.ID;
                            part.ID = UUID.Random;
                            sog.ChangeKey(part.ID, oldID);
                        }
                        foreach (ObjectPartInventoryItem item in part.Inventory.ValuesByKey2)
                        {
                            oldID = item.ID;
                            item.ID = UUID.Random;
                            part.Inventory.ChangeKey(item.ID, oldID);
                        }
                    }
                }
                else
                {
                    foreach (ObjectPart part in sog.ValuesByKey1)
                    {
                        UUID oldID = part.ID;
                        part.ID = UUID.Random;
                        sog.ChangeKey(part.ID, oldID);
                        foreach (ObjectPartInventoryItem item in part.Inventory.ValuesByKey2)
                        {
                            oldID = item.ID;
                            item.ID = UUID.Random;
                            part.Inventory.ChangeKey(item.ID, oldID);
                        }
                    }
                }
                scene.Add(sog);
            }
            sogs.Clear();
        }

        public static void Load(
            SceneInterface scene,
            LoadOptions options,
            Stream inputFile)
        {
            using (GZipStream gzipStream = new GZipStream(inputFile, CompressionMode.Decompress))
            {
                using (TarArchiveReader reader = new TarArchiveReader(gzipStream))
                {
                    GridVector baseLoc = new GridVector(0, 0);
                    if (scene != null)
                    {
                        baseLoc = scene.RegionData.Location;
                    }

                    GridVector regionSize = new GridVector(256, 256);
                    Dictionary<string, ArchiveXmlLoader.RegionInfo> regionMapping = new Dictionary<string, ArchiveXmlLoader.RegionInfo>();
                    List<ArchiveXmlLoader.RegionInfo> regionInfos = new List<ArchiveXmlLoader.RegionInfo>();
                    bool parcelsCleared = false;
                    List<ObjectGroup> load_sogs = new List<ObjectGroup>();

                    for (; ; )
                    {
                        TarArchiveReader.Header header;
                        try
                        {
                            header = reader.ReadHeader();
                        }
                        catch (TarArchiveReader.EndOfTarException)
                        {
                            if ((options & LoadOptions.Merge) == 0 && scene != null)
                            {
                                scene.ClearObjects();
                            }

                            AddObjects(scene, load_sogs, options);
                            return;
                        }

                        if (header.FileType == TarFileType.File)
                        {
                            if (header.FileName == "archive.xml")
                            {
                                ArchiveXmlLoader.RegionInfo rinfo = ArchiveXmlLoader.LoadArchiveXml(new ObjectXmlStreamFilter(reader), regionInfos);

                                regionSize = rinfo.RegionSize;
                                foreach (ArchiveXmlLoader.RegionInfo reginfo in regionInfos)
                                {
                                    regionMapping.Add(reginfo.Path, reginfo);
                                }
                                if (regionInfos.Count != 0 && scene != null)
                                {
                                    throw new MultiRegionOARLoadingTriedOnRegionException();
                                }
                                else if (regionInfos.Count == 0 && scene == null)
                                {
                                    throw new OARLoadingTriedWithoutSelectedRegionException();
                                }
                            }
                            else if (header.FileName.StartsWith("assets/"))
                            {
                                if ((options & LoadOptions.NoAssets) == 0)
                                {
                                    /* Load asset */
                                    AssetData ad = reader.LoadAsset(header, scene.Owner);
                                    if (!scene.AssetService.Exists(ad.ID))
                                    {
                                        scene.AssetService.Store(ad);
                                    }
                                }
                            }
                            else
                            {
                                if (header.FileName.StartsWith("regions/"))
                                {
                                    if ((options & LoadOptions.Merge) == 0 && scene != null)
                                    {
                                        scene.ClearObjects();
                                    }

                                    if (scene != null)
                                    {
                                        AddObjects(scene, load_sogs, options);
                                    }

                                    string[] pcomps = header.FileName.Split(new char[] { '/' }, 3);
                                    if (pcomps.Length < 3)
                                    {
                                        throw new OARFormatException();
                                    }
                                    string regionname = pcomps[1];
                                    header.FileName = pcomps[2];
                                    regionSize = regionMapping[regionname].RegionSize;
                                    scene = SceneManager.Scenes[regionMapping[regionname].ID];
                                    parcelsCleared = false;
                                }

                                if (header.FileName.StartsWith("objects/"))
                                {
                                    /* Load objects */
                                    List<ObjectGroup> sogs;
                                    try
                                    {
                                        sogs = ObjectXML.FromXml(reader, scene.Owner);
                                    }
                                    catch (Exception e)
                                    {
                                        throw new OARLoadingErrorException("Failed to load sog " + header.FileName, e);
                                    }

                                    foreach (ObjectGroup sog in sogs)
                                    {
                                        if (sog.Owner.ID == UUID.Zero)
                                        {
                                            sog.Owner = scene.Owner;
                                        }
                                    }
                                    load_sogs.AddRange(sogs);
                                }
                                else if (header.FileName.StartsWith("terrains/"))
                                {
                                    /* Load terrains */
                                    if ((options & LoadOptions.Merge) == 0)
                                    {
                                        scene.Terrain.AllPatches = TerrainLoader.LoadStream(reader, (int)regionSize.X, (int)regionSize.Y);
                                    }
                                }
                                else if (header.FileName.StartsWith("landdata/"))
                                {
                                    /* Load landdata */
                                    if ((options & LoadOptions.Merge) == 0)
                                    {
                                        if (!parcelsCleared)
                                        {
                                            scene.ClearParcels();
                                            parcelsCleared = true;
                                        }
                                        ParcelInfo pinfo = ParcelLoader.LoadParcel(new ObjectXmlStreamFilter(reader), regionSize);
                                        if (pinfo.Owner.ID == UUID.Zero)
                                        {
                                            pinfo.Owner = scene.Owner;
                                        }
                                        if ((options & LoadOptions.PersistUuids) == LoadOptions.PersistUuids)
                                        {
                                            ParcelInfo check;
                                            if(scene.Parcels.TryGetValue(pinfo.ID, out check))
                                            {
                                                pinfo.ID = UUID.Random;
                                            }
                                        }
                                        else
                                        {
                                            pinfo.ID = UUID.Random;
                                        }
                                        scene.AddParcel(pinfo);
                                    }
                                }
                                else if (header.FileName.StartsWith("settings/") && ((options & LoadOptions.Merge) == 0))
                                {
                                    /* Load settings */
                                    RegionSettingsLoader.LoadRegionSettings(new ObjectXmlStreamFilter(reader), scene);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
