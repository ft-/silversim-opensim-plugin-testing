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

using SilverSim.Main.Common.CmdIO;
using SilverSim.Main.Common.Tar;
using SilverSim.OpenSimArchiver.Common;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Asset.Format;
using SilverSim.Types.Inventory;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace SilverSim.OpenSimArchiver.InventoryArchiver
{
    public static partial class IAR
    {
        [Serializable]
        public class IARFormatException : Exception
        {
            public IARFormatException()
            {
            }

            public IARFormatException(string message)
                : base(message)
            {
            }

            public IARFormatException(string message, Exception innerException)
                : base(message, innerException)
            {
            }

            protected IARFormatException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }
        }

        [Serializable]
        public class InvalidInventoryPathException : Exception
        {
            public InvalidInventoryPathException()
            {
            }

            public InvalidInventoryPathException(string message)
                : base(message)
            {
            }

            public InvalidInventoryPathException(string message, Exception innerException)
                : base(message, innerException)
            {
            }

            protected InvalidInventoryPathException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }
        }

        [Flags]
        public enum LoadOptions
        {
            None = 0x00000000,
            Merge = 0x000000001,
            NoAssets = 0x00000002
        }

        public static void Load(
            UUI principal,
            InventoryServiceInterface inventoryService,
            AssetServiceInterface assetService,
            List<AvatarNameServiceInterface> nameServices,
            LoadOptions options,
            Stream inputFile,
            string topath,
            TTY console_io = null)
        {
            using (var gzipStream = new GZipStream(inputFile, CompressionMode.Decompress))
            {
                using (var reader = new TarArchiveReader(gzipStream))
                {
                    var inventoryPath = new Dictionary<UUID, UUID>();
                    var reassignedIds = new Dictionary<UUID, UUID>();
                    var linkItems = new List<InventoryItem>();

                    UUID parentFolder;
                    try
                    {
                        inventoryService.CheckInventory(principal.ID);
                    }
                    catch(NotSupportedException)
                    {
                        /* some handlers may not support this call, so ignore that error */
                    }
                    parentFolder = inventoryService.Folder[principal.ID, AssetType.RootFolder].ID;

                    if (!topath.StartsWith("/"))
                    {
                        throw new InvalidInventoryPathException();
                    }

                    if (topath != "/")
                    {
                        foreach (string pathcomp in topath.Substring(1).Split('/'))
                        {
                            List<InventoryFolder> childfolders = inventoryService.Folder.GetFolders(principal.ID, parentFolder);
                            int idx;
                            for (idx = 0; idx < childfolders.Count; ++idx)
                            {
                                if (pathcomp.ToLower() == childfolders[idx].Name.ToLower())
                                {
                                    break;
                                }
                            }

                            if (idx == childfolders.Count)
                            {
                                throw new InvalidInventoryPathException();
                            }

                            parentFolder = childfolders[idx].ID;
                        }
                    }

                    inventoryPath[UUID.Zero] = parentFolder;

                    for (; ; )
                    {
                        TarArchiveReader.Header header;
                        try
                        {
                            header = reader.ReadHeader();
                        }
                        catch (TarArchiveReader.EndOfTarException)
                        {
                            if(console_io != null)
                            {
                                console_io.Write("Creating link items");
                            }
                            foreach(InventoryItem linkitem in linkItems)
                            {
                                UUID newId;
                                if(linkitem.AssetType == AssetType.Link && reassignedIds.TryGetValue(linkitem.AssetID, out newId))
                                {
                                    linkitem.AssetID = newId;
                                }
                                inventoryService.Item.Add(linkitem);
                            }
                            return;
                        }

                        if (header.FileType == TarFileType.File)
                        {
                            if (header.FileName == "archive.xml")
                            {
                                using (Stream s = new ObjectXmlStreamFilter(reader))
                                {
                                    ArchiveXmlLoader.LoadArchiveXml(s);
                                }
                            }

                            if (header.FileName.StartsWith("assets/") && (options & LoadOptions.NoAssets) == 0)
                            {
                                /* Load asset */
                                AssetData ad = reader.LoadAsset(header, principal);
                                if (!assetService.Exists(ad.ID))
                                {
                                    assetService.Store(ad);
                                }
                            }

                            if (header.FileName.StartsWith("inventory/"))
                            {
                                /* Load inventory */
                                InventoryItem item = LoadInventoryItem(reader, principal, nameServices);
                                item.ParentFolderID = GetPath(principal, inventoryService, inventoryPath, header.FileName, options);

                                UUID oldId = item.ID;
                                item.SetNewID(UUID.Random);
                                reassignedIds.Add(oldId, item.ID);

                                if (item.AssetType == AssetType.Link || item.AssetType == AssetType.LinkFolder)
                                {
                                    inventoryService.Item.Add(item);
                                }
                                else
                                {
                                    linkItems.Add(item);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static UUID GetPath(
            UUI principalID,
            InventoryServiceInterface inventoryService,
            Dictionary<UUID, UUID> folders,
            string path,
            LoadOptions options)
        {
            path = path.Substring(10); /* Get Rid of inventory/ */
            path = path.Substring(0, path.LastIndexOf('/'));
            string[] pathcomps = path.Split('/');
            var finalpath = new StringBuilder();
            UUID folderID = folders[string.Empty];

            int pathidx = 0;
            if ((options & LoadOptions.Merge) != 0 &&
                pathcomps[0].StartsWith("MyInventory") &&
                pathcomps[0].Length == 13 + 36)
            {
                pathidx = 1;
            }

            for(; pathidx < pathcomps.Length; ++pathidx)
            {
                if(finalpath.Length != 0)
                {
                    finalpath.Append("/");
                }

                string uuidstr = pathcomps[pathidx].Substring(pathcomps[pathidx].Length - 36);
                UUID nextfolderid = UUID.Parse(uuidstr);
                string pname = pathcomps[pathidx].Substring(0, pathcomps[pathidx].Length - 38);
                finalpath.Append(pname);
                if(folders.ContainsKey(nextfolderid))
                {
                    folderID = folders[nextfolderid];
                }
                else
                {
                    var folder = new InventoryFolder()
                    {
                        Owner = principalID,
                        ParentFolderID = folderID,
                        Name = pname
                    };
                    inventoryService.Folder.Add(folder);
                    folderID = folder.ID;
                    folders[nextfolderid] = folderID;
                }
            }
            return folderID;
        }

        private static InventoryItem LoadInventoryItem(
            Stream s,
            UUI principal,
            List<AvatarNameServiceInterface> nameServices)
        {
            using (XmlTextReader reader = new XmlTextReader(new ObjectXmlStreamFilter(s)))
            {
                for(;;)
                {
                    if(!reader.Read())
                    {
                        throw new IARFormatException();
                    }

                    switch(reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if(reader.Name == "InventoryItem")
                            {
                                return LoadInventoryItemData(reader, principal, nameServices);
                            }
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        private static InventoryItem LoadInventoryItemData(
            XmlTextReader reader,
            UUI principal,
            List<AvatarNameServiceInterface> nameServices)
        {
            var item = new InventoryItem()
            {
                Owner = principal
            };
            for (;;)
            {
                if(!reader.Read())
                {
                    throw new IARFormatException();
                }

                switch(reader.NodeType)
                {
                    case XmlNodeType.Element:
                        switch(reader.Name)
                        {
                            case "Name":
                                item.Name = reader.ReadElementValueAsString();
                                break;

                            case "InvType":
                                item.InventoryType = (InventoryType)reader.ReadElementValueAsInt();
                                break;

                            case "CreatorUUID":
                                {
                                    string text = reader.ReadElementValueAsString();
                                    UUID uuid;
                                    if(text.StartsWith("ospa:n="))
                                    {
                                        string[] name = text.Substring(7).Split(' ');
                                        /* OpenSim tag version */
                                        item.Creator.ID = UUID.Zero;
                                        item.Creator.FirstName = name[0];
                                        if(name.Length > 1)
                                        {
                                            item.Creator.LastName = name[1];
                                        }

                                        /* hope that name service knows that avatar */
                                        try
                                        {
                                            item.Creator = nameServices.FindUUIByName(item.Creator.FirstName, item.Creator.LastName);
                                        }
                                        catch
                                        {
                                            item.Creator = principal;
                                        }
                                    }
                                    else if(UUID.TryParse(text, out uuid))
                                    {
                                        try
                                        {
                                            item.Creator = nameServices.FindUUIById(uuid);
                                        }
                                        catch
                                        {
                                            item.Creator.ID = uuid;
                                        }
                                    }
                                    else
                                    {
                                        item.Creator = principal;
                                    }
                                }
                                break;

                            case "CreatorData":
                                {
                                    string creatorData = reader.ReadElementValueAsString();
                                    try
                                    {
                                        item.Creator.CreatorData = creatorData;
                                    }
                                    catch
                                    {
                                        /* ignore misformatted creator data */
                                    }
                                }
                                break;

                            case "CreationDate":
                                item.CreationDate = Date.UnixTimeToDateTime(reader.ReadElementValueAsULong());
                                break;

                            case "Description":
                                item.Description = reader.ReadElementValueAsString();
                                break;

                            case "AssetType":
                                item.AssetType = (AssetType)reader.ReadElementValueAsInt();
                                break;

                            case "AssetID":
                                item.AssetID = UUID.Parse(reader.ReadElementValueAsString());
                                break;

                            case "SalePrice":
                                item.SaleInfo.Price = reader.ReadElementValueAsInt();
                                break;

                            case "SaleType":
                                item.SaleInfo.Type = (InventoryItem.SaleInfoData.SaleType)reader.ReadElementValueAsUInt();
                                break;

                            case "BasePermissions":
                                item.Permissions.Base = (InventoryPermissionsMask)reader.ReadElementValueAsUInt();
                                break;

                            case "CurrentPermissions":
                                item.Permissions.Current = (InventoryPermissionsMask)reader.ReadElementValueAsUInt();
                                break;

                            case "EveryOnePermissions":
                                item.Permissions.EveryOne = (InventoryPermissionsMask)reader.ReadElementValueAsUInt();
                                break;

                            case "NextPermissions":
                                item.Permissions.NextOwner = (InventoryPermissionsMask)reader.ReadElementValueAsUInt();
                                break;

                            case "Flags":
                                item.Flags = (InventoryFlags)reader.ReadElementValueAsUInt();
                                break;

                            case "GroupID":
                                item.Group.ID = reader.ReadElementValueAsString();
                                break;

                            case "GroupOwned":
                                item.IsGroupOwned = reader.ReadElementValueAsBoolean();
                                break;

                            case "ID":
                                item.SetNewID(UUID.Parse(reader.ReadElementValueAsString()));
                                break;

                            case "Owner":
                            default:
                                if(!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
                                break;
                        }
                        break;

                    case XmlNodeType.EndElement:
                        if(reader.Name != "InventoryItem")
                        {
                            throw new IARFormatException();
                        }
                        return item;

                    default:
                        break;
                }
            }
        }
    }
}
