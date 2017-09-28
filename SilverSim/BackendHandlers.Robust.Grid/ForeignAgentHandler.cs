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

using Nini.Config;
using SilverSim.BackendConnectors.Robust.StructuredData.Agent;
using SilverSim.BackendConnectors.Robust.UserAgent;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.UserAgents;
using System.ComponentModel;

namespace SilverSim.BackendHandlers.Robust.Grid
{
    [PluginName("ForeignAgentHandler")]
    [Description("Robust Foreign Agent Handler Service")]
    public class ForeignAgentHandler : GridPostAgentHandler
    {
        public ForeignAgentHandler(IConfig ownSection) 
            : base("/foreignagent/", ownSection)
        {
        }

        public override bool TryVerifyIdentity(HttpRequest req, PostData agentPost)
        {
            UserAgentServiceInterface userAgentService = new RobustUserAgentConnector(agentPost.Account.Principal.HomeURI.ToString());

            try
            {
                userAgentService.VerifyAgent(agentPost.Session.SessionID, agentPost.Session.ServiceSessionID);
            }
            catch
            {
                m_Log.InfoFormat("Failed to verify agent {0} at Home Grid (Code 1)", agentPost.Account.Principal.FullName);
                DoAgentResponse(req, "Failed to verify agent at Home Grid (Code 1)", false);
                return false;
            }

            try
            {
                userAgentService.VerifyClient(agentPost.Session.SessionID, agentPost.Client.ClientIP);
            }
            catch
            {
                m_Log.InfoFormat("Failed to verify client {0} at Home Grid (Code 2)", agentPost.Account.Principal.FullName);
                DoAgentResponse(req, "Failed to verify client at Home Grid (Code 2)", false);
                return false;
            }

            return true;
        }
    }
}
