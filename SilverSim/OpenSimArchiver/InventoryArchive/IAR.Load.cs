// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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
        }

        [Serializable]
        public class InvalidInventoryPathException : Exception
        {
            public InvalidInventoryPathException()
            {

            }
        }

        [Flags]
        public enum LoadOptions
        {
            Merge = 0x000000001,
            NoAssets = 0x00000002
        }

        public static void Load(
            UUI principal, 
            InventoryServiceInterface inventoryService,
            AssetServiceInterface assetService,
            AvatarNameServiceInterface nameService,
            LoadOptions options,
            Stream inputFile,
            string topath)
        {
            using (GZipStream gzipStream = new GZipStream(inputFile, CompressionMode.Decompress))
            {
                using (TarArchiveReader reader = new TarArchiveReader(gzipStream))
                {
                    Dictionary<string, UUID> inventoryPath = new Dictionary<string, UUID>();

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
                            return;
                        }

                        if (header.FileType == TarFileType.File)
                        {
                            if (header.FileName == "archive.xml")
                            {
                                ArchiveXmlLoader.LoadArchiveXml(new ObjectXmlStreamFilter(reader));
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
                                InventoryItem item = LoadInventoryItem(reader, principal, nameService);
                                item.ParentFolderID = GetPath(principal, inventoryService, inventoryPath, header.FileName, options);
                                inventoryService.Item.Add(item);
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
            string finalpath = string.Empty;
            UUID folderID = folders[string.Empty];


            int pathidx = 0;
            if ((options & LoadOptions.Merge) != 0)
            {
                if (pathcomps[0].StartsWith("MyInventory") && pathcomps[0].Length == 13 + 36)
                {
                    pathidx = 1;
                }
            }

            for(; pathidx < pathcomps.Length; ++pathidx)
            {
                if(finalpath.Length != 0)
                {
                    finalpath += "/";
                }
                string pname = pathcomps[pathidx].Substring(0, pathcomps[pathidx].Length - 38);
                finalpath += pname;
                if (folders.TryGetValue(finalpath, out folderID))
                {

                }
                else
                {
                    InventoryFolder folder = new InventoryFolder();
                    folder.Owner = principalID;
                    folder.ParentFolderID = folderID;
                    folder.Name = pname;
                    inventoryService.Folder.Add(folder);
                    folderID = folder.ID;
                    folders[finalpath] = folderID;
                }
            }
            return folderID;
        }

        static InventoryItem LoadInventoryItem(
            Stream s, 
            UUI principal,
            AvatarNameServiceInterface nameService)
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
                                return LoadInventoryItemData(reader, principal, nameService);
                            }
                            break;
                    }
                }
            }
        }

        static InventoryItem LoadInventoryItemData(
            XmlTextReader reader, 
            UUI principal,
            AvatarNameServiceInterface nameService)
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

                            case "ID":
                                if(!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
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
                                            item.Creator = nameService[item.Creator.FirstName, item.Creator.LastName];
                                        }
                                        catch
                                        {
                                            item.Creator = principal;
                                        }
                                    }
                                    else if(UUID.TryParse(text, out uuid))
                                    {
                                        item.Creator.ID = uuid;
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

                            case "Owner":
                                if(!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
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
                                item.Flags = reader.ReadElementValueAsUInt();
                                break;

                            case "GroupID":
                                item.Group.ID = reader.ReadElementValueAsString();
                                break;

                            case "GroupOwned":
                                item.IsGroupOwned = reader.ReadElementValueAsBoolean();
                                break;

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
                }
            }
        }
    }
}
