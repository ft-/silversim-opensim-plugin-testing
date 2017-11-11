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

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Http.Client;
using SilverSim.ServiceInterfaces.Friends;
using SilverSim.Types;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.FriendsStatus
{
    [Description("OpenSim HGFriends Connector")]
    public sealed class RobustHGFriendsStatusNotifyService : IFriendsStatusNotifyServiceInterface
    {
        public int TimeoutMs { get; set; }

        private readonly string m_Url;
        public RobustHGFriendsStatusNotifyService(string url)
        {
            m_Url = url;
            if(!m_Url.EndsWith("/"))
            {
                m_Url += "/";
            }
            m_Url += "hgfriends";
            TimeoutMs = 20000;
        }

        public void NotifyAsOffline(UUI notifier, List<KeyValuePair<UUI, string>> list) => Notify(notifier, list, false);

        public void NotifyAsOnline(UUI notifier, List<KeyValuePair<UUI, string>> list) => Notify(notifier, list, true);

        private void Notify(UUI notifier, List<KeyValuePair<UUI, string>> list, bool isOnline)
        {
            var postvals = new Dictionary<string, string>();
            postvals.Add("METHOD", "statusnotification");
            postvals.Add("userID", notifier.ID.ToString());
            postvals.Add("online", isOnline.ToString());
            int friendcnt = 0;
            foreach(KeyValuePair<UUI, string> kvp in list)
            {
                postvals.Add($"friend_{friendcnt++}", $"{kvp.Key};${kvp.Value}");
            }

            Map res;
            using (Stream s = new HttpClient.Post(m_Url, postvals)
            {
                TimeoutMs = TimeoutMs
            }.ExecuteStreamRequest())
            {
                res = OpenSimResponse.Deserialize(s);
            }

            foreach(KeyValuePair<string, IValue> kvp in res)
            {
                UUID id;
                if(kvp.Key.StartsWith("friend_") && UUID.TryParse(kvp.Value.ToString(), out id))
                {

                }
            }
        }
    }
}
