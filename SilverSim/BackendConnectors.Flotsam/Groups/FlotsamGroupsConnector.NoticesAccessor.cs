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
        List<GroupNotice> IGroupNoticesInterface.GetNotices(UUI requestingAgent, UGI group)
        {
            Map m = new Map();
            m.Add("GroupID", group.ID);
            AnArray r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotices", m) as AnArray;
            if(null == r)
            {
                throw new AccessFailedException();
            }
            List<GroupNotice> notices = new List<GroupNotice>();
            foreach(IValue iv in r)
            {
                Map data = iv as Map;
                if(null != data)
                {
                    notices.Add(data.ToGroupNotice());
                }
            }
            return notices;
        }

        bool IGroupNoticesInterface.TryGetValue(UUI requestingAgent, UUID groupNoticeID, out GroupNotice notice)
        {
            Map m = new Map();
            m.Add("NoticeID", groupNoticeID);
            Map r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotice", m) as Map;
            if (null == r)
            {
                notice = default(GroupNotice);
                return false;
            }
            notice = r.ToGroupNotice();
            return true;
        }

        bool IGroupNoticesInterface.ContainsKey(UUI requestingAgent, UUID groupNoticeID)
        {
            Map m = new Map();
            m.Add("NoticeID", groupNoticeID);
            Map r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotice", m) as Map;
            return r != null;
        }

        GroupNotice IGroupNoticesInterface.this[UUI requestingAgent, UUID groupNoticeID]
        {
            get 
            {
                Map m = new Map();
                m.Add("NoticeID", groupNoticeID);
                Map r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotice", m) as Map;
                if(null == r)
                {
                    throw new InvalidDataException();
                }
                return r.ToGroupNotice();
            }
        }

        void IGroupNoticesInterface.Add(UUI requestingAgent, GroupNotice notice)
        {
            Map m = new Map();
            m.Add("GroupID", notice.Group.ID);
            m.Add("NoticeID", notice.ID);
            m.Add("FromName", notice.FromName);
            m.Add("Subject", notice.Subject);
#warning TODO: Binary Bucket conversion
            m.Add("BinaryBucket", new BinaryData());
            m.Add("Message", notice.Message);
            m.Add("TimeStamp", notice.Timestamp.AsULong.ToString());
            FlotsamXmlRpcCall(requestingAgent, "groups.addGroupNotice", m);
        }

        void IGroupNoticesInterface.Delete(UUI requestingAgent, UUID groupNoticeID)
        {
            throw new NotImplementedException();
        }
    }
}
