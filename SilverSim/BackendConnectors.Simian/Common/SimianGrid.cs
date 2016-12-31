﻿// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Http.Client;
using SilverSim.Types.StructuredData.Llsd;
using SilverSim.Types;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Simian.Common
{
    public static class SimianGrid
    {
        public static Map PostToService(string serverUrl, string capability, Dictionary<string, string> requestargs, bool compressed, int timeoutms = 100000)
        {
            requestargs["cap"] = capability;
            using(Stream s = HttpClient.DoStreamPostRequest(serverUrl, null, requestargs, compressed, timeoutms))
            {
                return (Map)LlsdXml.Deserialize(s);
            }
        }

        public static Map PostToService(string serverUrl, string capability, Dictionary<string, string> requestargs, int timeoutms = 100000)
        {
            requestargs["cap"] = capability;
            using(Stream s = HttpClient.DoStreamPostRequest(serverUrl, null, requestargs, false, timeoutms))
            {
                return (Map)LlsdXml.Deserialize(s);
            }
        }
    }
}
