// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.CmdIO;
using SilverSim.Main.Common.Tar;
using SilverSim.Scene.Types.Object;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace SilverSim.OpenSimArchiver.InventoryArchiver
{
    public static partial class IAR
    {
        [Flags]
        public enum SaveOptions
        {
            None = 0x00000000,
            NoAssets = 0x00000001
        }

        static byte[] WriteArchiveXml(bool assetsIncluded)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (XmlTextWriter writer = ms.UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("archive");
                    writer.WriteAttributeString("major_version", "0");
                    writer.WriteAttributeString("minor_version", "8");
                    {
                        writer.WriteNamedValue("assets_included", assetsIncluded);
                    }
                    writer.WriteEndElement();
                    writer.Flush();

                    return ms.ToArray();
                }
            }
        }

        static byte[] WriteInventoryItem(InventoryItem item)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (XmlTextWriter writer = ms.UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("InventoryItem");
                    writer.WriteNamedValue("Name", item.Name);
                    writer.WriteNamedValue("ID", item.ID);
                    writer.WriteNamedValue("InvType", (int)item.InventoryType);
                    writer.WriteNamedValue("CreatorUUID", string.Format("ospa:n={0} {1}", item.Creator.FirstName, item.Creator.LastName));
                    writer.WriteNamedValue("CreatorData", item.Creator.CreatorData);
                    writer.WriteNamedValue("CreationDate", item.CreationDate.AsULong);
                    writer.WriteNamedValue("Owner", item.Owner.ID);
                    writer.WriteNamedValue("Description", item.Description);
                    writer.WriteNamedValue("AssetType", (int)item.AssetType);
                    writer.WriteNamedValue("AssetID", item.AssetID);
                    writer.WriteNamedValue("SaleType", (int)item.SaleInfo.Type);
                    writer.WriteNamedValue("SalePrice", item.SaleInfo.Price);
                    writer.WriteNamedValue("BasePermissions", (uint)item.Permissions.Base);
                    writer.WriteNamedValue("CurrentPermissions", (uint)item.Permissions.Current);
                    writer.WriteNamedValue("EveryOnePermissions", (uint)item.Permissions.EveryOne);
                    writer.WriteNamedValue("NextPermissions", (uint)item.Permissions.NextOwner);
                    writer.WriteNamedValue("Flags", (uint)item.Flags);
                    writer.WriteNamedValue("GroupID", item.Group.ID);
                    writer.WriteNamedValue("GroupOwned", item.IsGroupOwned);
                    writer.WriteNamedValue("CreatorID", item.Creator.ID);
                    writer.WriteEndElement();
                }
                return ms.ToArray();
            }
        }

        public static void Save(
            UUI principal,
            InventoryServiceInterface inventoryService,
            AssetServiceInterface assetService,
            List<AvatarNameServiceInterface> nameServices,
            SaveOptions options,
            string fileName,
            string frompath,
            TTY console_io = null)
        {
            UUID parentFolder;
            parentFolder = inventoryService.Folder[principal.ID, AssetType.RootFolder].ID;

            if (!frompath.StartsWith("/"))
            {
                throw new InvalidInventoryPathException();
            }
            foreach (string pathcomp in frompath.Substring(1).Split('/'))
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

        static string GetFolderPath(Dictionary<UUID, KeyValuePair<string, UUID>> folders, InventoryItem item)
        {
            UUID parentFolderId = item.ParentFolderID;
            StringBuilder sb = new StringBuilder(item.Name + "__" + item.ID.ToString() + ".xml");
            while(UUID.Zero != parentFolderId)
            {
                KeyValuePair<string, UUID> data = folders[parentFolderId];
                sb.Insert(0, data.Key + "__" + parentFolderId + "/");
            }
            return sb.ToString();
        }

        public static void Save(
            UUI principal,
            InventoryServiceInterface inventoryService,
            AssetServiceInterface assetService,
            List<AvatarNameServiceInterface> nameServices,
            SaveOptions options,
            Stream outputFile,
            string frompath,
            TTY console_io = null)
        {
            UUID parentFolder;
            Dictionary<UUID, KeyValuePair<string, UUID>> folders = new Dictionary<UUID, KeyValuePair<string, UUID>>();
            parentFolder = inventoryService.Folder[principal.ID, AssetType.RootFolder].ID;

            if (!frompath.StartsWith("/"))
            {
                throw new InvalidInventoryPathException();
            }
            foreach (string pathcomp in frompath.Substring(1).Split('/'))
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
                folders[parentFolder] = new KeyValuePair<string, UUID>(childfolders[idx].Name, UUID.Zero);
            }

            using (GZipStream gzip = new GZipStream(outputFile, CompressionMode.Compress))
            {
                TarArchiveWriter writer = new TarArchiveWriter(gzip);

                bool saveAssets = (options & SaveOptions.NoAssets) == 0;

                if (console_io != null)
                {
                    console_io.Write("Saving archive info...");
                }

                writer.WriteFile("archive.xml", WriteArchiveXml(saveAssets));

                List<UUID> nextFolders = new List<UUID>();
                List<UUID> assetIds = new List<UUID>();
                nextFolders.Add(parentFolder);
                if (console_io != null)
                {
                    console_io.Write("Saving inventory data...");
                }

                while (nextFolders.Count != 0)
                {
                    InventoryFolderContent content;
                    UUID folderId = nextFolders[0];
                    nextFolders.RemoveAt(0);
                    content = inventoryService.Folder.Content[principal.ID, folderId];
                    foreach(InventoryFolder folder in content.Folders)
                    {
                        folders[folder.ID] = new KeyValuePair<string, UUID>(folder.Name, folderId);
                    }

                    foreach (InventoryItem item in content.Items)
                    {
                        if(item.AssetType != AssetType.Link && item.AssetType != AssetType.LinkFolder &&
                            !assetIds.Contains(item.AssetID) && saveAssets)
                        {
                            assetIds.Add(item.AssetID);
                        }

                        writer.WriteFile(GetFolderPath(folders, item), WriteInventoryItem(item));
                    }
                }

                if (saveAssets)
                {
                    if (console_io != null)
                    {
                        console_io.Write("Saving asset data...");
                    }
                    /* we only parse sim details when saving assets */
                    AssetData data;

                    int assetidx = 0;
                    while (assetidx < assetIds.Count)
                    {
                        UUID assetID = assetIds[assetidx++];
                        try
                        {
                            data = assetService[assetID];
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
                                if (!assetIds.Contains(refid))
                                {
                                    assetIds.Add(refid);
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
            }
        }
    }
}
