// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

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
