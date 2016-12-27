// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.CmdIO;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.ServiceInterfaces.Presence;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.OpenSimArchiver
{
    [Description("IAR Plugin")]
    public class NpcInventoryArchiverLoadStore : IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("NPC-IAR ARCHIVER");
        InventoryServiceInterface m_NpcInventoryService;
        NpcPresenceServiceInterface m_NpcPresenceService;
        AssetServiceInterface m_NpcAssetService;
        readonly List<AvatarNameServiceInterface> m_AvatarNameServices = new List<AvatarNameServiceInterface>();

        readonly string m_NpcInventoryServiceName;
        readonly string m_NpcPresenceServiceName;
        readonly string m_NpcAssetServiceName;
        readonly string m_AvatarNameServiceNames;

        public NpcInventoryArchiverLoadStore(IConfig ownSection)
        {
            m_NpcPresenceServiceName = ownSection.GetString("NpcPresenceService");
            m_NpcInventoryServiceName = ownSection.GetString("NpcInventoryService");
            m_NpcAssetServiceName = ownSection.GetString("NpcAssetService", "AssetService");
            m_AvatarNameServiceNames = ownSection.GetString("AvatarNameServices", "AvatarNameStorage");
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_NpcInventoryService = loader.GetService<InventoryServiceInterface>(m_NpcInventoryServiceName);
            m_NpcPresenceService = loader.GetService<NpcPresenceServiceInterface>(m_NpcPresenceServiceName);
            m_NpcAssetService = loader.GetService<AssetServiceInterface>(m_NpcAssetServiceName);
            foreach (string avatarNameService in m_AvatarNameServiceNames.Split(','))
            {
                if (!string.IsNullOrEmpty(avatarNameService))
                {
                    m_AvatarNameServices.Add(loader.GetService<AvatarNameServiceInterface>(avatarNameService));
                }
            }
            loader.CommandRegistry.AddLoadCommand("npc-iar", LoadIarCommand);
            loader.CommandRegistry.AddSaveCommand("npc-iar", SaveIarCommand);
        }

        #region Save IAR
        void SaveIarCommand(List<string> args, TTY io, UUID limitedToScene)
        {
            if (args[0] == "help")
            {
                string outp = "Available commands:\n";
                outp += "save npc-iar [--noassets] <firstname> <lastname> <inventorypath> <filename>\n";
                io.Write(outp);
                return;
            }

            UUID selectedScene = io.SelectedScene;
            if (limitedToScene != UUID.Zero)
            {
                selectedScene = limitedToScene;
            }

            if (UUID.Zero == selectedScene)
            {
                io.Write("No scene selected");
                return;
            }

            string firstname = null;
            string lastname = null;
            string filename = null;
            string inventorypath = null;
            InventoryArchiver.IAR.SaveOptions options = InventoryArchiver.IAR.SaveOptions.None;

            for (int argi = 2; argi < args.Count; ++argi)
            {
                string arg = args[argi];
                if (arg == "--noassets")
                {
                    options |= InventoryArchiver.IAR.SaveOptions.NoAssets;
                }
                else if (firstname == null)
                {
                    firstname = arg;
                }
                else if (lastname == null)
                {
                    lastname = arg;
                }
                else if (inventorypath == null)
                {
                    inventorypath = arg;
                }
                else
                {
                    filename = arg;
                }
            }

            if (null == filename)
            {
                io.Write("missing parameters");
                return;
            }

            NpcPresenceInfo presence;
            if(!m_NpcPresenceService.TryGetValue(selectedScene, firstname, lastname, out presence))
            {
                io.Write("Persistent npc not found");
                return;
            }

            try
            {
                using (Stream s = new FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    InventoryArchiver.IAR.Save(presence.Npc, m_NpcInventoryService, m_NpcAssetService, m_AvatarNameServices, options, filename, inventorypath, io);
                }
                io.Write("IAR saved successfully.");
            }
            catch (Exception e)
            {
                io.WriteFormatted("IAR saving failed: {0}", e.Message);
            }
        }
        #endregion

        #region Load IAR
        void LoadIarCommand(List<string> args, TTY io, UUID limitedToScene)
        {
            if (args[0] == "help")
            {
                string outp = "Available commands:\n";
                outp += "load npc-iar [-m|--merge] [--noassets] <firstname> <lastname> <inventorypath> <filename>\n";
                io.Write(outp);
                return;
            }

            UUID selectedScene = io.SelectedScene;
            if (limitedToScene != UUID.Zero)
            {
                selectedScene = limitedToScene;
            }

            if (UUID.Zero == selectedScene)
            {
                io.Write("No scene selected");
                return;
            }


            string filename = null;
            string firstname = null;
            string lastname = null;
            string inventorypath = null;
            InventoryArchiver.IAR.LoadOptions options = InventoryArchiver.IAR.LoadOptions.None;

            for (int argi = 2; argi < args.Count; ++argi)
            {
                string arg = args[argi];
                if (arg == "--skip-assets")
                {
                    options |= InventoryArchiver.IAR.LoadOptions.NoAssets;
                }
                else if (arg == "--merge" || arg == "-m")
                {
                    options |= InventoryArchiver.IAR.LoadOptions.Merge;
                }
                else if (firstname == null)
                {
                    firstname = arg;
                }
                else if (lastname == null)
                {
                    lastname = arg;
                }
                else if (inventorypath == null)
                {
                    inventorypath = arg;
                }
                else
                {
                    filename = arg;
                }
            }

            if (string.IsNullOrEmpty(filename))
            {
                io.Write("No filename or url specified.\n");
                return;
            }

            NpcPresenceInfo presenceInfo;
            if(!m_NpcPresenceService.TryGetValue(selectedScene, firstname, lastname, out presenceInfo))
            {
                io.Write("Persistent npc not found");
                return;
            }

            try
            {
                using (Stream s = Uri.IsWellFormedUriString(filename, UriKind.Absolute) ?
                    HttpRequestHandler.DoStreamGetRequest(filename, null, 20000) :
                    new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    InventoryArchiver.IAR.Load(presenceInfo.Npc, m_NpcInventoryService, m_NpcAssetService, m_AvatarNameServices, options, s, inventorypath, io);
                }
                io.Write("IAR loaded successfully.");
            }
            catch (InventoryArchiver.IAR.IARFormatException)
            {
                io.Write("IAR file is corrupt");
            }
            catch (Exception e)
            {
                m_Log.Info("IAR load exception encountered", e);
                io.Write(e.Message);
            }
        }
        #endregion

    }
}
