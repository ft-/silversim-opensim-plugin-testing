﻿// SilverSim is distributed under the terms of the
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

using Nini.Config;
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.Traveling;
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.TravelingData;

namespace SilverSim.BackendHandlers.Robust.Grid
{
    [PluginName("HomeAgentHandler")]
    public class HomeAgentHandler : GridPostAgentHandler
    {
        private readonly string m_UserAccountServiceName;
        private readonly string m_TravelingDataServiceName;
        private readonly string m_HomeURI;
        UserAccountServiceInterface m_UserAccountService;
        TravelingDataServiceInterface m_TravelingDataService;

        public HomeAgentHandler(IConfig ownConfig)
            : base("/homeagent/", ownConfig)
        {
            m_UserAccountServiceName = ownConfig.GetString("UserAccountService", "UserAccountService");
            m_TravelingDataServiceName = ownConfig.GetString("TravelingDataService", "TravelingDataService");
            m_HomeURI = ownConfig.GetString("HomeURI");
        }

        public override void Startup(ConfigurationLoader loader)
        {
            base.Startup(loader);
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
        }

        public override bool TryVerifyIdentity(HttpRequest req, PostData data)
        {
            if(data.Account.Principal.HomeURI == null || data.Account.Principal.HomeURI.ToString() != m_HomeURI)
            {
                m_Log.InfoFormat("Failed to verify agent {0} at Home Grid (Code 1)", data.Account.Principal.FullName);
                DoAgentResponse(req, "Failed to verify agent at Home Grid (Code 1)", false);
                return false;
            }

            UserAccount account;
            if(!m_UserAccountService.TryGetValue(UUID.Zero, data.Account.Principal.ID, out account))
            {
                m_Log.InfoFormat("Failed to verify agent {0} at Home Grid (Code 2)", data.Account.Principal.FullName);
                DoAgentResponse(req, "Failed to verify agent at Home Grid (Code 2)", false);
                return false;
            }

            TravelingDataInfo tInfo;

            try
            {
                tInfo = m_TravelingDataService.GetTravelingData(data.Account.Principal.ID);
            }
            catch
            {
                m_Log.InfoFormat("Failed to verify agent {0} at Home Grid (Code 3)", data.Account.Principal.FullName);
                DoAgentResponse(req, "Failed to verify agent at Home Grid (Code 3)", false);
                return false;
            }

            if(tInfo.ClientIPAddress != data.Client.ClientIP)
            {
                /* TODO: add NATting */
                m_Log.InfoFormat("Failed to verify agent {0} at Home Grid (Code 4)", data.Account.Principal.FullName);
                DoAgentResponse(req, "Failed to verify agent at Home Grid (Code 4)", false);
                return false;
            }

            data.Account.Principal = account.Principal;
            return true;
        }
    }
}