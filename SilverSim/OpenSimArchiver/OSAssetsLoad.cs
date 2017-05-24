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
    [PluginName("OSAssetsArchiveSupport")]
    public sealed class OSAssetsLoad : IPlugin
    {
        private SceneList m_Scenes;

        public void Startup(ConfigurationLoader loader)
        {
            m_Scenes = loader.Scenes;
            loader.CommandRegistry.AddLoadCommand("osassets", LoadAssetsCommand);
        }

        #region Load Assets
        private void LoadAssetsCommand(List<string> args, TTY io, UUID limitedToScene)
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
