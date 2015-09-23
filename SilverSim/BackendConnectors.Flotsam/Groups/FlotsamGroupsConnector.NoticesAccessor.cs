// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Groups;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector
    {
        class NoticesAccessor : FlotsamGroupsCommonConnector, IGroupNoticesInterface
        {
            public NoticesAccessor(string uri)
                : base(uri)
            {
            }

            public List<GroupNotice> GetNotices(UUI requestingAgent, UGI group)
            {
                Map m = new Map();
                m.Add("GroupID", group.ID);
                IValue r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotices", m);
                if(!(r is AnArray))
                {
                    throw new AccessFailedException();
                }
                List<GroupNotice> notices = new List<GroupNotice>();
                foreach(IValue iv in (AnArray)r)
                {
                    if(iv is Map)
                    {
                        notices.Add(iv.ToGroupNotice());
                    }
                }
                return notices;
            }

            public GroupNotice this[UUI requestingAgent, UUID groupNoticeID]
            {
                get 
                {
                    Map m = new Map();
                    m.Add("NoticeID", groupNoticeID);
                    IValue r = FlotsamXmlRpcGetCall(requestingAgent, "groups.getGroupNotice", m);
                    return r.ToGroupNotice();
                }
            }

            public void Add(UUI requestingAgent, GroupNotice notice)
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

            public void Delete(UUI requestingAgent, UUID groupNoticeID)
            {
                throw new NotImplementedException();
            }
        }
    }
}
