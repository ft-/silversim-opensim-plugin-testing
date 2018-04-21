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

using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector : GroupsServiceInterface.IGroupNoticesInterface
    {
        List<GroupNotice> IGroupNoticesInterface.GetNotices(UGUI requestingAgent, UGI group)
        {
            var m = new Map
            {
                ["GroupID"] = group.ID
            };
            var r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotices", m) as AnArray;
            if(r == null)
            {
                throw new AccessFailedException();
            }
            var notices = new List<GroupNotice>();
            foreach(IValue iv in r)
            {
                var data = iv as Map;
                if(data != null)
                {
                    notices.Add(data.ToGroupNotice());
                }
            }
            return notices;
        }

        bool IGroupNoticesInterface.TryGetValue(UGUI requestingAgent, UUID groupNoticeID, out GroupNotice notice)
        {
            var m = new Map
            {
                ["NoticeID"] = groupNoticeID
            };
            var r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotice", m) as Map;
            if (r == null)
            {
                notice = default(GroupNotice);
                return false;
            }
            notice = r.ToGroupNotice();
            return true;
        }

        bool IGroupNoticesInterface.ContainsKey(UGUI requestingAgent, UUID groupNoticeID)
        {
            var m = new Map
            {
                ["NoticeID"] = groupNoticeID
            };
            var r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotice", m) as Map;
            return r != null;
        }

        GroupNotice IGroupNoticesInterface.this[UGUI requestingAgent, UUID groupNoticeID]
        {
            get
            {
                var m = new Map
                {
                    ["NoticeID"] = groupNoticeID
                };
                var r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotice", m) as Map;
                if(r == null)
                {
                    throw new InvalidDataException();
                }
                return r.ToGroupNotice();
            }
        }

        void IGroupNoticesInterface.Add(UGUI requestingAgent, GroupNotice notice)
        {
#warning TODO: Binary Bucket conversion
            var m = new Map
            {
                { "GroupID", notice.Group.ID },
                { "NoticeID", notice.ID },
                { "FromName", notice.FromName },
                { "Subject", notice.Subject },
                { "BinaryBucket", new BinaryData() },
                { "Message", notice.Message },
                { "TimeStamp", notice.Timestamp.AsULong.ToString() }
            };
            FlotsamXmlRpcCall(requestingAgent, "groups.addGroupNotice", m);
        }

        void IGroupNoticesInterface.Delete(UGUI requestingAgent, UUID groupNoticeID)
        {
            throw new NotImplementedException();
        }
    }
}
