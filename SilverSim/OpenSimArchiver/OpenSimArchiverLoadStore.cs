// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.CmdIO;
using SilverSim.Scene.Management.Scene;
using SilverSim.Scene.Types.Scene;
using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.OpenSimArchiver
{
    [Description("OAR Plugin")]
    public sealed class OpenSimArchiverLoadStore : IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("OAR ARCHIVER");
        SceneList m_Scenes;

        public OpenSimArchiverLoadStore()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Scenes = loader.Scenes;
            loader.CommandRegistry.AddLoadCommand("oar", LoadOarCommand);
            loader.CommandRegistry.AddSaveCommand("oar", SaveOarCommand);
        }

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

            if (UUID.Zero == selectedScene)
            {
                io.Write("Multi-region OARs currently not supported");
                return;
            }
            else
            {
                try
                {
                    scene = m_Scenes[selectedScene];
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
                    RegionArchiver.OAR.Save(scene, options, s, io);
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

            if (UUID.Zero != selectedScene)
            {
                try
                {
                    scene = m_Scenes[selectedScene];
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
                else if (arg == "--persist-uuids")
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
                    RegionArchiver.OAR.Load(m_Scenes, scene, options, s, io);
                }
                io.Write("OAR loaded successfully.");
            }
            catch (RegionArchiver.OAR.OARLoadingTriedWithoutSelectedRegionException)
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
