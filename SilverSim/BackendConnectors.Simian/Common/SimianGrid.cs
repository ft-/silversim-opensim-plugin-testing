// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common.HttpClient;
using SilverSim.StructuredData.LLSD;
using SilverSim.Types;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Simian.Common
{
    public static class SimianGrid
    {
        public static Map PostToService(string serverUrl, string capability, Dictionary<string, string> requestargs, bool compressed, int timeoutms = 100000)
        {
            requestargs["cap"] = capability;
            return (Map)LLSD_XML.Deserialize(HttpRequestHandler.DoStreamPostRequest(serverUrl, null, requestargs, compressed, timeoutms));
        }

        public static Map PostToService(string serverUrl, string capability, Dictionary<string, string> requestargs, int timeoutms = 100000)
        {
            requestargs["cap"] = capability;
            return (Map)LLSD_XML.Deserialize(HttpRequestHandler.DoStreamPostRequest(serverUrl, null, requestargs, false, timeoutms));
        }
    }
}
