// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.CmdIO;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Scene;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SilverSim.OpenSimArchiver
{
    [Description("IAR/OAR Plugin")]
    public sealed class ArchiverLoadStore : IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("IAR/OAR ARCHIVER");

        public ArchiverLoadStore()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
            CommandRegistry.LoadCommands.Add("oar", LoadOarCommand);
            CommandRegistry.SaveCommands.Add("oar", SaveOarCommand);
            CommandRegistry.LoadCommands.Add("osassets", LoadAssetsCommand);
        }

        #region Load Assets
        public void LoadAssetsCommand(List<string> args, TTY io, UUID limitedToScene)
        {
            if(args[0] == "help")
            {
                string outp = "Available commands:\n";
                outp += "load osassets <filename> - Load assets to scene\n";
                io.Write(outp);
                return;
            }

            UUID selectedScene = io.SelectedScene;
            if (limitedToScene != UUID.Zero)
            {
                selectedScene = limitedToScene;
            }

            AssetServiceInterface assetService;
            UUI owner;

            if(args.Count == 3)
            {
                /* scene */
                if(selectedScene == UUID.Zero)
                {
                    io.Write("No region selected");
                    return;
                }
                else
                {
                    try
                    {
                        SceneInterface scene = SceneManager.Scenes[selectedScene];
                        assetService = scene.AssetService;
                        owner = scene.Owner;
                    }
                    catch
                    {
                        io.Write("Selected region not found");
                        return;
                    }
                }
            }
            else
            {
                io.Write("Invalid arguments to load osassets");
                return;
            }

            try
            {
                using (Stream s = Uri.IsWellFormedUriString(args[2], UriKind.Absolute) ?
                    HttpRequestHandler.DoStreamGetRequest(args[2], null, 20000) :
                    new FileStream(args[2], FileMode.Open, FileAccess.Read))
                {
                    Assets.AssetsLoad.Load(assetService, owner, s);
                }
                io.Write("Assets loaded successfully.");
            }
            catch (Exception e)
            {
                io.Write(e.Message);
            }
        }
        #endregion

        #region Save OAR
        public void SaveOarCommand(List<string> args, TTY io, UUID limitedToScene)
        {
            if (args[0] == "help")
            {
                string outp = "Available commands:\n";
                outp += "save oar [--publish] [--noassets] <filename>\n";
                io.Write(outp);
                return;
            }

            UUID selectedScene = io.SelectedScene;
            SceneInterface scene;
            if (limitedToScene != UUID.Zero)
            {
                selectedScene = limitedToScene;
            }

            if(UUID.Zero == selectedScene)
            {
                io.Write("Multi-region OARs currently not supported");
                return;
            }
            else
            {
                try
                {
                    scene = SceneManager.Scenes[selectedScene];
                }
                catch
                {
                    io.Write("Selected region does not exist");
                    return;
                }
            }

            string filename = null;
            RegionArchiver.OAR.SaveOptions options = RegionArchiver.OAR.SaveOptions.None;

            for (int argi = 2; argi < args.Count; ++argi)
            {
                string arg = args[argi];
                if (arg == "--noassets")
                {
                    options |= RegionArchiver.OAR.SaveOptions.NoAssets;
                }
                else if (arg == "--publish")
                {
                    options |= RegionArchiver.OAR.SaveOptions.Publish;
                }
                else
                {
                    filename = arg;
                }
            }

            try
            {
                using (Stream s = new FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    RegionArchiver.OAR.Save(scene, options, s);
                }
                io.Write("OAR saved successfully.");
            }
            catch (Exception e)
            {
                io.Write(e.Message);
            }
        }
        #endregion

        #region Load OAR
        public void LoadOarCommand(List<string> args, TTY io, UUID limitedToScene)
        {
            if (args[0] == "help")
            {
                string outp = "Available commands:\n";
                outp += "load oar [--skip-assets] [--merge] [--persist-uuids] <filename>\n";
                outp += "load oar [--skip-assets] [--merge] [--persist-uuids] <url>\n\n";
                outp += "--persist-uuids cannot be combined with --merge\n";
                io.Write(outp);
                return;
            }

            UUID selectedScene = io.SelectedScene;
            SceneInterface scene = null;
            if (limitedToScene != UUID.Zero)
            {
                selectedScene = limitedToScene;
            }

            if(UUID.Zero != selectedScene)
            {
                try
                {
                    scene = SceneManager.Scenes[selectedScene];
                }
                catch
                {
                    io.Write("Selected scene does not exist.");
                    return;
                }
            }

            string filename = null;
            RegionArchiver.OAR.LoadOptions options = RegionArchiver.OAR.LoadOptions.None;

            for (int argi = 2; argi < args.Count; ++argi)
            {
                string arg = args[argi];
                if (arg == "--skip-assets")
                {
                    options |= RegionArchiver.OAR.LoadOptions.NoAssets;
                }
                else if (arg == "--merge")
                {
                    options |= RegionArchiver.OAR.LoadOptions.Merge;
                }
                else if(arg == "--persist-uuids")
                {
                    options |= RegionArchiver.OAR.LoadOptions.PersistUuids;
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

            try
            {
                using (Stream s = Uri.IsWellFormedUriString(filename, UriKind.Absolute) ?
                    HttpRequestHandler.DoStreamGetRequest(filename, null, 20000) :
                    new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    RegionArchiver.OAR.Load(scene, options, s);
                }
                io.Write("OAR loaded successfully.");
            }
            catch(RegionArchiver.OAR.OARLoadingTriedWithoutSelectedRegionException)
            {
                io.Write("No region selected");
            }
            catch (RegionArchiver.OAR.MultiRegionOARLoadingTriedOnRegionException)
            {
                io.Write("Multi-Region OAR cannot be loaded with a selected region");
            }
            catch (RegionArchiver.OAR.OARFormatException)
            {
                io.Write("OAR file is corrupt");
            }
            catch (Exception e)
            {
                m_Log.Info("OAR load exception encountered", e);
                io.Write(e.Message);
            }
        }
        #endregion
    }
}
