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
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Types;
using SilverSim.Types.Account;
using SilverSim.Types.Groups;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace SilverSim.BackendConnectors.Robust.GroupsV2
{
    [Description("Robust Groups Connector")]
    public partial class RobustGroupsConnector : GroupsServiceInterface, IPlugin
    {
        private readonly GroupsAccessor m_Groups;
        private readonly GroupRolesAccessor m_GroupRoles;
        private readonly MembersAccessor m_Members;
        private readonly MembershipsAccessor m_Memberships;
        private readonly RoleMembersAccessor m_Rolemembers;
        private readonly ActiveGroupAccessor m_ActiveGroup;
        private readonly InvitesAccessor m_Invites;
        private readonly NoticesAccessor m_Notices;
        private readonly ActiveGroupMembershipAccesor m_ActiveGroupMembership;
        private UserAccountServiceInterface m_UserAccountService;
        private readonly string m_UserAccountServiceName = string.Empty;
        private int m_TimeoutMs = 20000;

        public int TimeoutMs
        {
            get { return m_TimeoutMs; }

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

        private string GetGroupsAgentID(UUI agent)
        {
            if (m_UserAccountService == null)
            {
                return agent.ToString();
            }
            try
            {
                UserAccount account = m_UserAccountService[UUID.Zero, agent.ID];
                return (string)account.Principal.ID;
            }
            catch
            {
                return agent.ToString();
            }
        }

        public RobustGroupsConnector(string uri, string userAccountServiceName)
        {
            if(!uri.EndsWith("/"))
            {
                uri += "/";
            }
            uri += "groups";
            m_UserAccountServiceName = userAccountServiceName;
            m_Groups = new GroupsAccessor(uri, GetGroupsAgentID);
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

        public override IGroupsInterface Groups => m_Groups;

        public override IGroupRolesInterface Roles => m_GroupRoles;

        public override IGroupMembersInterface Members => m_Members;

        public override IGroupMembershipsInterface Memberships => m_Memberships;

        public override IGroupRolemembersInterface Rolemembers => m_Rolemembers;

        public override IGroupSelectInterface ActiveGroup => m_ActiveGroup;

        public override IActiveGroupMembershipInterface ActiveMembership => m_ActiveGroupMembership;

        public override IGroupInvitesInterface Invites => m_Invites;

        public override IGroupNoticesInterface Notices => m_Notices;

        internal static void BooleanResponseRequest(string uri, Dictionary<string, string> post, bool compressed, int timeoutms)
        {
            Map m;
            using(Stream s = HttpClient.DoStreamPostRequest(uri, null, post, compressed, timeoutms))
            {
                m = OpenSimResponse.Deserialize(s);
            }
            if(!m.ContainsKey("RESULT"))
            {
                throw new AccessFailedException();
            }
            if(m["RESULT"].ToString().ToLower() != "true")
            {
                throw new AccessFailedException();
            }
        }

        public override GroupInfo CreateGroup(UUI requestingAgent, GroupInfo ginfo, GroupPowers everyonePowers, GroupPowers ownerPowers) =>
            Groups.Create(requestingAgent, ginfo);
    }
}
