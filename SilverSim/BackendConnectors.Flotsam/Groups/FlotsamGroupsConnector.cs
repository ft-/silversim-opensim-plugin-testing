// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Main.Common;
using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Types;
using SilverSim.Types.StructuredData.XMLRPC;
using System;
using System.Collections.Generic;

/*
 * RPC method names:
 * 
 * "groups.createGroup"
 * "groups.updateGroup"
 * "groups.getGroup"
 * "groups.findGroups"
 * "groups.getGroupRoles"
 * "groups.addRoleToGroup"
 * "groups.removeRoleFromGroup"
 * "groups.updateGroupRole"
 * "groups.getGroupRoleMembers"
 *
 * "groups.setAgentGroupSelectedRole" 
 * "groups.addAgentToGroupRole"       
 * "groups.removeAgentFromGroupRole"  
 * 
 * "groups.getGroupMembers"           
 * "groups.addAgentToGroup"           
 * "groups.removeAgentFromGroup"      
 * "groups.setAgentGroupInfo"         
 * "groups.addAgentToGroupInvite"     
 * "groups.getAgentToGroupInvite"     
 * "groups.removeAgentToGroupInvite"  
 * 
 * "groups.setAgentActiveGroup"       
 * "groups.getAgentGroupMembership"   
 * "groups.getAgentGroupMemberships"  
 * "groups.getAgentActiveMembership"  
 * "groups.getAgentRoles"             
 * 
 * "groups.getGroupNotices"           
 * "groups.getGroupNotice"            
 * "groups.addGroupNotice"            
 */

namespace SilverSim.BackendConnectors.Flotsam.Groups
{
    public partial class FlotsamGroupsConnector : GroupsServiceInterface, IPlugin
    {
        GroupsAccessor m_Groups;
        GroupRolesAccessor m_GroupRoles;
        MembersAccessor m_Members;
        RoleMembersAccessor m_Rolemembers;
        ActiveGroupAccessor m_ActiveGroup;
        MembershipsAccessor m_Memberships;
        InvitesAccessor m_Invites;
        NoticesAccessor m_Notices;
        ActiveGroupMembershipAccessor m_ActiveGroupMembership;
        int m_TimeoutMs = 20000;

        public int TimeoutMs
        {
            get
            {
                return m_TimeoutMs;
            }
            set
            {
                m_TimeoutMs = value;
                m_Groups.TimeoutMs = value;
                m_GroupRoles.TimeoutMs = value;
                m_Members.TimeoutMs = value;
                m_Memberships.TimeoutMs = value;
                m_Rolemembers.TimeoutMs = value;
                m_ActiveGroup.TimeoutMs = value;
                m_Invites.TimeoutMs = value;
                m_Notices.TimeoutMs = value;
                m_ActiveGroupMembership.TimeoutMs = value;
            }
        }

        public FlotsamGroupsConnector(string uri)
        {
            m_Groups = new GroupsAccessor(uri);
            m_GroupRoles = new GroupRolesAccessor(uri);
            m_Members = new MembersAccessor(uri);
            m_Memberships = new MembershipsAccessor(uri);
            m_Rolemembers = new RoleMembersAccessor(uri);
            m_ActiveGroup = new ActiveGroupAccessor(uri);
            m_Invites = new InvitesAccessor(uri);
            m_Notices = new NoticesAccessor(uri);
            m_ActiveGroupMembership = new ActiveGroupMembershipAccessor(uri);
        }

        public void Startup(ConfigurationLoader loader)
        {
        }

        public class FlotsamGroupsCommonConnector
        {
            protected string m_Uri;
            public int TimeoutMs = 20000;
            string m_ReadKey = string.Empty;
            string m_WriteKey = string.Empty;

            public FlotsamGroupsCommonConnector(string uri)
            {
                m_Uri = uri;
            }

            protected IValue FlotsamXmlRpcCall(UUI requestingAgent, string methodName, Map structparam)
            {
                XMLRPC.XmlRpcRequest req = new XMLRPC.XmlRpcRequest();
                req.MethodName = methodName;
                structparam.Add("RequestingAgentID", requestingAgent.ID);
                structparam.Add("RequestingAgentUserService", requestingAgent.HomeURI);
                structparam.Add("RequestingSessionID", UUID.Zero);
                structparam.Add("ReadKey", m_ReadKey);
                structparam.Add("WriteKey", m_WriteKey);
                req.Params.Add(structparam);
                XMLRPC.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);
                if (!(res.ReturnValue is Map))
                {
                    throw new Exception("Unexpected FlotsamGroups return value");
                }
                Map p = (Map)res.ReturnValue;
                if (!p.ContainsKey("success"))
                {
                    throw new Exception("Unexpected FlotsamGroups return value");
                }

                if (p["success"].ToString().ToLower() != "true")
                {
                    throw new KeyNotFoundException();
                }
                if (p.ContainsKey("results"))
                {
                    return p["results"];
                }
                else
                {
                    return null; /* some calls have no data */
                }
            }

            protected IValue FlotsamXmlRpcGetCall(UUI requestingAgent, string methodName, Map structparam)
            {
                XMLRPC.XmlRpcRequest req = new XMLRPC.XmlRpcRequest();
                req.MethodName = methodName;
                structparam.Add("RequestingAgentID", requestingAgent.ID);
                structparam.Add("RequestingAgentUserService", requestingAgent.HomeURI);
                structparam.Add("RequestingSessionID", UUID.Zero);
                structparam.Add("ReadKey", m_ReadKey);
                structparam.Add("WriteKey", m_WriteKey);
                req.Params.Add(structparam);
                XMLRPC.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);
                if (res.ReturnValue is Map)
                {
                    Map p = (Map)res.ReturnValue;
                    if (p.ContainsKey("error"))
                    {
                        throw new Exception("Unexpected FlotsamGroups return value");
                    }
                }
                
                return res.ReturnValue;
            }

        }


        public override IGroupsInterface Groups
        {
            get
            {
                return m_Groups;
            }
        }

        public override IGroupRolesInterface Roles
        {
            get
            {
                return m_GroupRoles;
            }
        }

        public override IGroupMembersInterface Members
        {
            get
            {
                return m_Members;
            }
        }

        public override IGroupMembershipsInterface Memberships
        {
            get 
            {
                return m_Memberships;
            }
        }

        public override IGroupRolemembersInterface Rolemembers
        {
            get
            {
                return m_Rolemembers;
            }
        }

        public override IGroupSelectInterface ActiveGroup
        {
            get
            {
                return m_ActiveGroup;
            }
        }

        public override IActiveGroupMembershipInterface ActiveMembership
        {
            get 
            {
                return m_ActiveGroupMembership;
            }
        }

        public override IGroupInvitesInterface Invites
        {
            get
            {
                return m_Invites;
            }
        }

        public override IGroupNoticesInterface Notices
        {
            get
            {
                return m_Notices;
            }
        }
    }
}
