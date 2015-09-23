// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.BackendConnectors.Robust.Common;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpClient;
using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Types;
using SilverSim.Types.Account;
using System;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    public partial class RobustGroupsConnector : GroupsServiceInterface, IPlugin
    {
        GroupsAccessor m_Groups;
        GroupRolesAccessor m_GroupRoles;
        MembersAccessor m_Members;
        MembershipsAccessor m_Memberships;
        RoleMembersAccessor m_Rolemembers;
        ActiveGroupAccessor m_ActiveGroup;
        InvitesAccessor m_Invites;
        NoticesAccessor m_Notices;
        ActiveGroupMembershipAccesor m_ActiveGroupMembership;
        UserAccountServiceInterface m_UserAccountService;
        string m_UserAccountServiceName = "";
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

        string GetGroupsAgentID(UUI agent)
        {
            if (null == m_UserAccountService)
            {
                return agent.ToString();
            }
            try
            {
                UserAccount account = m_UserAccountService[UUID.Zero, agent.ID];
                return (string)agent.ID;
            }
            catch
            {
                return agent.ToString();
            }
        }

        public delegate string GetGroupsAgentIDDelegate(UUI agent);

        public RobustGroupsConnector(string uri, string serviceUri, string userAccountServiceName)
        {
            if(!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "groups";
            m_UserAccountServiceName = userAccountServiceName;
            m_Groups = new GroupsAccessor(uri, serviceUri, GetGroupsAgentID);
            m_GroupRoles = new GroupRolesAccessor(uri, GetGroupsAgentID);
            m_Members = new MembersAccessor(uri, GetGroupsAgentID);
            m_Memberships = new MembershipsAccessor(uri, GetGroupsAgentID);
            m_ActiveGroup = new ActiveGroupAccessor(uri, GetGroupsAgentID);
            m_Invites = new InvitesAccessor(uri, GetGroupsAgentID);
            m_Notices = new NoticesAccessor(uri, GetGroupsAgentID);
            m_ActiveGroupMembership = new ActiveGroupMembershipAccesor(uri, GetGroupsAgentID);
            m_Rolemembers = new RoleMembersAccessor(uri, m_Memberships, GetGroupsAgentID);
        }

        public void Startup(ConfigurationLoader loader)
        {
            if(!string.IsNullOrEmpty(m_UserAccountServiceName))
            {
                m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
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

        internal static void BooleanResponseRequest(string uri, Dictionary<string, string> post, bool compressed, int timeoutms)
        {
            Map m = OpenSimResponse.Deserialize(HttpRequestHandler.DoStreamPostRequest(uri, null, post, compressed, timeoutms));
            if(!m.ContainsKey("RESULT"))
            {
                throw new AccessFailedException();
            }
            if(m["RESULT"].ToString().ToLower() != "true")
            {
                throw new AccessFailedException();
            }
        }
    }
}
