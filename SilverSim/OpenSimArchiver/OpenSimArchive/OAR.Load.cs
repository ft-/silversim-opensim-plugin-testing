﻿// SilverSim is distributed under the terms of the
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

using SilverSim.Main.Common.CmdIO;
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
        public enum LoadOptions
        {
            None = 0,
            Merge = 0x000000001,
            NoAssets = 0x00000002,
            PersistUuids = 0x00000004,
        }

        private static void AddObjects(
            SceneInterface scene,
            List<ObjectGroup> sogs,
            LoadOptions options)
        {
            foreach (ObjectGroup sog in sogs)
            {
                sog.LastOwner = scene.AvatarNameService.ResolveName(sog.LastOwner);
                sog.Owner = scene.AvatarNameService.ResolveName(sog.Owner);
                foreach (ObjectPart part in sog.ValuesByKey1)
                {
                    part.Creator = scene.AvatarNameService.ResolveName(part.Creator);
                    foreach(ObjectPartInventoryItem item in part.Inventory.ValuesByKey1)
                    {
                        item.Owner = scene.AvatarNameService.ResolveName(item.Owner);
                        item.Creator = scene.AvatarNameService.ResolveName(item.Creator);
                        item.LastOwner = scene.AvatarNameService.ResolveName(item.LastOwner);
                    }
                }
                scene.Add(sog);
            }
            foreach (ObjectGroup grp in sogs)
            {
                scene.RezScriptsForObject(grp);
            }
            sogs.Clear();
        }

        private enum CurrentOarLoadState
        {
            Unknown,
            Assets,
            Objects,
            Terrain,
            RegionSettings,
            Parcels,
            Region
        }

        private static void ShowOarLoadState(ref CurrentOarLoadState currentState, CurrentOarLoadState newState, TTY io)
        {
            if(currentState != newState)
            {
                if (io != null)
                {
                    switch (newState)
                    {
                        case CurrentOarLoadState.Assets:
                            io.Write("Loading assets");
                            break;
                        case CurrentOarLoadState.Objects:
                            io.Write("Loading objects");
                            break;
                        case CurrentOarLoadState.RegionSettings:
                            io.Write("Loading region settings");
                            break;
                        case CurrentOarLoadState.Terrain:
                            io.Write("Loading terrain");
                            break;
                        case CurrentOarLoadState.Region:
                            io.Write("Loading region");
                            break;
                        case CurrentOarLoadState.Parcels:
                            io.Write("Loading parcels");
                            break;
                        default:
                            break;
                    }
                }
                currentState = newState;
            }
        }

        public static void Load(
            SceneList scenes,
            SceneInterface scene,
            LoadOptions options,
            Stream inputFile,
            TTY io = null)
        {
            var currentLoadState = CurrentOarLoadState.Unknown;

            using (var gzipStream = new GZipStream(inputFile, CompressionMode.Decompress))
            {
                using (var reader = new TarArchiveReader(gzipStream))
                {
                    var baseLoc = new GridVector(0, 0);
                    if (scene != null)
                    {
                        baseLoc = scene.GridPosition;
                    }

                    var regionSize = new GridVector(256, 256);
                    var regionMapping = new Dictionary<string, ArchiveXmlLoader.RegionInfo>();
                    var regionInfos = new List<ArchiveXmlLoader.RegionInfo>();
                    bool parcelsCleared = false;
                    var load_sogs = new List<ObjectGroup>();

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
                                    ShowOarLoadState(ref currentLoadState, CurrentOarLoadState.Assets, io);
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
                                    ShowOarLoadState(ref currentLoadState, CurrentOarLoadState.Region, io);
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
                                    scene = scenes[regionMapping[regionname].ID];
                                    parcelsCleared = false;
                                }

                                if (header.FileName.StartsWith("objects/"))
                                {
                                    ShowOarLoadState(ref currentLoadState, CurrentOarLoadState.Objects, io);
                                    /* Load objects */
                                    List<ObjectGroup> sogs;
                                    XmlDeserializationOptions xmloptions = XmlDeserializationOptions.ReadKeyframeMotion;
                                    if((options & LoadOptions.PersistUuids) != 0)
                                    {
                                        xmloptions |= XmlDeserializationOptions.RestoreIDs;
                                    }
                                    try
                                    {
                                        sogs = ObjectXML.FromXml(reader, scene.Owner, xmloptions);
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
                                    ShowOarLoadState(ref currentLoadState, CurrentOarLoadState.Terrain, io);
                                    /* Load terrains */
                                    if ((options & LoadOptions.Merge) == 0)
                                    {
                                        scene.Terrain.AllPatches = TerrainLoader.LoadStream(reader, (int)regionSize.X, (int)regionSize.Y);
                                        scene.StoreTerrainAsDefault();
                                    }
                                }
                                else if (header.FileName.StartsWith("landdata/"))
                                {
                                    ShowOarLoadState(ref currentLoadState, CurrentOarLoadState.Parcels, io);
                                    /* Load landdata */
                                    if ((options & LoadOptions.Merge) == 0)
                                    {
                                        if (!parcelsCleared)
                                        {
                                            scene.ClearParcels();
                                            parcelsCleared = true;
                                        }
                                        var whiteList = new List<ParcelAccessEntry>();
                                        var blackList = new List<ParcelAccessEntry>();
                                        ParcelInfo pinfo = ParcelLoader.GetParcelInfo(new ObjectXmlStreamFilter(reader), regionSize, whiteList, blackList);
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
                                        scene.Parcels.WhiteList.Remove(scene.ID, pinfo.ID);
                                        scene.Parcels.BlackList.Remove(scene.ID, pinfo.ID);
                                        foreach(ParcelAccessEntry pae in whiteList)
                                        {
                                            scene.Parcels.WhiteList.Store(pae);
                                        }
                                        foreach(ParcelAccessEntry pae in blackList)
                                        {
                                            scene.Parcels.BlackList.Store(pae);
                                        }
                                    }
                                }
                                else if (header.FileName.StartsWith("settings/") && ((options & LoadOptions.Merge) == 0))
                                {
                                    ShowOarLoadState(ref currentLoadState, CurrentOarLoadState.Parcels, io);
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
