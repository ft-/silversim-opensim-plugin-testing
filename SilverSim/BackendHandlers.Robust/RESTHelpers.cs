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

using SilverSim.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace SilverSim.BackendHandlers.Robust
{
    [Serializable]
    public class FailureResultException : Exception
    {
        public FailureResultException()
        {

        }

        public FailureResultException(string message)
            : base(message)
        {

        }

        public FailureResultException(string message, Exception innerException)
            : base(message, innerException)
        {

        }

        protected FailureResultException(SerializationInfo info, StreamingContext context)
            : base(info, context)
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
