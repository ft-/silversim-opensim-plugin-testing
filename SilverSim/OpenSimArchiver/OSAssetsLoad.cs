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
using System.IO;

namespace SilverSim.OpenSimArchiver
{
    [Description("OS Assets Load Plugin")]
    public sealed class OSAssetsLoad : IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("OSASSETS LOAD ARCHIVER");
        SceneList m_Scenes;

        public OSAssetsLoad()
        {

        }

        public void Startup(ConfigurationLoader loader)
        {
            m_Scenes = loader.Scenes;
            loader.CommandRegistry.AddLoadCommand("osassets", LoadAssetsCommand);
        }

        #region Load Assets
        public void LoadAssetsCommand(List<string> args, TTY io, UUID limitedToScene)
        {
            if (args[0] == "help")
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

            if (args.Count == 3)
            {
                /* scene */
                if (selectedScene == UUID.Zero)
                {
                    io.Write("No region selected");
                    return;
                }
                else
                {
                    try
                    {
                        SceneInterface scene = m_Scenes[selectedScene];
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
                    HttpClient.DoStreamGetRequest(args[2], null, 20000) :
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
    }
}
