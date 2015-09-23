// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilverSim.BackendHandlers.Robust
{
    public class FailureResultException : Exception
    {
        public FailureResultException()
        {

        }
    }

    public static class RESTHelpers
    {
        public static int GetInt(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
            {
                throw new FailureResultException();
            }
            try
            {
                return int.Parse(dict[key].ToString());
            }
            catch
            {
                throw new FailureResultException();
            }
        }

        public static uint GetUInt(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
            {
                throw new FailureResultException();
            }
            try
            {
                return uint.Parse(dict[key].ToString());
            }
            catch
            {
                throw new FailureResultException();
            }
        }

        public static ulong GetULong(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
            {
                throw new FailureResultException();
            }
            try
            {
                return ulong.Parse(dict[key].ToString());
            }
            catch
            {
                throw new FailureResultException();
            }
        }

        public static string GetString(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
            {
                throw new FailureResultException();
            }
            return dict[key].ToString();
        }

        public static List<string> GetList(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
            {
                throw new FailureResultException();
            }
            if (!(dict[key] is List<string>))
            {
                throw new FailureResultException();
            }
            return (List<string>)dict[key];
        }

        public static List<UUID> GetUUIDList(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
            {
                throw new FailureResultException();
            }
            if (!(dict[key] is List<string>))
            {
                throw new FailureResultException();
            }
            List<UUID> uuids = new List<UUID>();
            foreach (string s in (List<string>)dict[key])
            {
                UUID o;
                if (!UUID.TryParse(s, out o))
                {
                    throw new FailureResultException();
                }
                uuids.Add(o);
            }
            return uuids;
        }

        public static UUID GetUUID(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
            {
                throw new FailureResultException();
            }
            try
            {
                return new UUID(dict[key].ToString());
            }
            catch
            {
                throw new FailureResultException();
            }
        }
    }
}
