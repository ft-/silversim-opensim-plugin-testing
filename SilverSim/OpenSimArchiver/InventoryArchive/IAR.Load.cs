// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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
            using (GZipStream gzipStream = new GZipStream(inputFile, CompressionMode.Decompress))
            {
                using (TarArchiveReader reader = new TarArchiveReader(gzipStream))
                {
                    Dictionary<string, UUID> inventoryPath = new Dictionary<string, UUID>();
                    Dictionary<UUID, UUID> reassignedIds = new Dictionary<UUID, UUID>();
                    List<InventoryItem> linkItems = new List<InventoryItem>();

                    UUID parentFolder;
                    parentFolder = inventoryService.Folder[principal.ID, AssetType.RootFolder].ID;

                    if (!topath.StartsWith("/"))
                    {
                        throw new InvalidInventoryPathException();
                    }
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

                    inventoryPath[string.Empty] = parentFolder;

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
                                item.ID = UUID.Random;
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

        static UUID GetPath(
            UUI principalID, 
            InventoryServiceInterface inventoryService, 
            Dictionary<string, UUID> folders, 
            string path, 
            LoadOptions options)
        {
            path = path.Substring(10); /* Get Rid of inventory/ */
            path = path.Substring(0, path.LastIndexOf('/'));
            string[] pathcomps = path.Split('/');
            StringBuilder finalpath = new StringBuilder();
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
                string pname = pathcomps[pathidx].Substring(0, pathcomps[pathidx].Length - 38);
                finalpath.Append(pname);
                if (!folders.TryGetValue(finalpath.ToString(), out folderID))
                {
                    InventoryFolder folder = new InventoryFolder();
                    folder.Owner = principalID;
                    folder.ParentFolderID = folderID;
                    folder.Name = pname;
                    inventoryService.Folder.Add(folder);
                    folderID = folder.ID;
                    folders[finalpath.ToString()] = folderID;
                }
            }
            return folderID;
        }

        static InventoryItem LoadInventoryItem(
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

        static InventoryItem LoadInventoryItemData(
            XmlTextReader reader, 
            UUI principal,
            List<AvatarNameServiceInterface> nameServices)
        {
            InventoryItem item = new InventoryItem();
            item.Owner = principal;

            for(;;)
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
                                item.ID = UUID.Parse(reader.ReadElementValueAsString());
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
