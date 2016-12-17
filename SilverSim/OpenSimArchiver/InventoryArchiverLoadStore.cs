// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.Http.Client;
using SilverSim.Main.Common;
using SilverSim.Main.Common.CmdIO;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.Asset;
using SilverSim.ServiceInterfaces.AuthInfo;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Account;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.OpenSimArchiver
{
    [Description("IAR Plugin")]
    public sealed class InventoryArchiverLoadStore : IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("IAR ARCHIVER");
        AuthInfoServiceInterface m_AuthInfoService;
        AssetServiceInterface m_AssetService;
        InventoryServiceInterface m_InventoryService;
        UserAccountServiceInterface m_UserAccountService;
        List<AvatarNameServiceInterface> m_AvatarNameServices = new List<AvatarNameServiceInterface>();

        readonly string m_AuthInfoServiceName;
        readonly string m_AssetServiceName;
        readonly string m_InventoryServiceName;
        readonly string m_UserAccountServiceName;
        readonly string m_AvatarNameServiceNames;

        public InventoryArchiverLoadStore(IConfig ownSection)
        {
            m_AuthInfoServiceName = ownSection.GetString("AuthInfoService", "AuthInfoService");
            m_AssetServiceName = ownSection.GetString("AssetService", "AssetService");
            m_InventoryServiceName = ownSection.GetString("InventoryService", "InventoryService");
            m_UserAccountServiceName = ownSection.GetString("UserAccountService", "UserAccountService");
            m_AvatarNameServiceNames = ownSection.GetString("AvatarNameServices", string.Empty);
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_AuthInfoService = loader.GetService<AuthInfoServiceInterface>(m_AuthInfoServiceName);
            m_AssetService = loader.GetService<AssetServiceInterface>(m_AssetServiceName);
            m_InventoryService = loader.GetService<InventoryServiceInterface>(m_InventoryServiceName);
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
            foreach(string avatarNameService in m_AvatarNameServiceNames.Split(','))
            {
                if (!string.IsNullOrEmpty(avatarNameService))
                {
                    m_AvatarNameServices.Add(loader.GetService<AvatarNameServiceInterface>(avatarNameService));
                }
            }
        }

        UserAccount Authenticate(string firstName, string lastName, string password)
        {
            UserAccount accountInfo = m_UserAccountService[UUID.Zero, firstName, lastName];
            m_AuthInfoService.Authenticate(UUID.Zero, accountInfo.Principal.ID, password, 30);

            return accountInfo;
        }

        #region Save IAR
        void SaveIarCommand(List<string> args, TTY io, UUID limitedToScene)
        {
            if (args[0] == "help")
            {
                string outp = "Available commands:\n";
                outp += "save iar [--noassets] <firstname> <lastname> <inventorypath> <filename>\n";
                io.Write(outp);
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
                else if(firstname == null)
                {
                    firstname = arg;
                }
                else if(lastname == null)
                {
                    lastname = arg;
                }
                else if(inventorypath == null)
                {
                    inventorypath = arg;
                }
                else
                {
                    filename = arg;
                }
            }

            if(null == filename)
            {
                io.Write("missing parameters");
                return;
            }

            UserAccount account;
            try
            {
                account = Authenticate(firstname, lastname, io.GetPass("Password"));
            }
            catch(Exception e)
            {
                io.WriteFormatted("failed to authenticate: {0}", e.Message);
                return;
            }

            try
            {
                using (Stream s = new FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    InventoryArchiver.IAR.Save(account.Principal, m_InventoryService, m_AssetService, m_AvatarNameServices, options, filename, inventorypath, io);
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
                outp += "load iar [-m|--merge] [--noassets] <firstname> <lastname> <inventorypath> <filename>\n";
                io.Write(outp);
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
                else if(firstname == null)
                {
                    firstname = arg;
                }
                else if(lastname == null)
                {
                    lastname = arg;
                }
                else if(inventorypath == null)
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

            UserAccount account;
            try
            {
                account = Authenticate(firstname, lastname, io.GetPass("Password"));
            }
            catch (Exception e)
            {
                io.WriteFormatted("failed to authenticate: {0}", e.Message);
                return;
            }

            try
            {
                using (Stream s = Uri.IsWellFormedUriString(filename, UriKind.Absolute) ?
                    HttpRequestHandler.DoStreamGetRequest(filename, null, 20000) :
                    new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    InventoryArchiver.IAR.Load(account.Principal, m_InventoryService, m_AssetService, m_AvatarNameServices, options, s, inventorypath, io);
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
