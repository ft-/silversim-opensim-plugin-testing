// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.CmdIO;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System;
using System.Collections.Generic;

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
    }
}
