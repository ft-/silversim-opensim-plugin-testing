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

using log4net;
using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.Rpc;
using SilverSim.ServiceInterfaces.AvatarName;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.Threading;
using SilverSim.Types;
using SilverSim.Types.StructuredData.XmlRpc;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

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
    [Description("XmlRpc Groups Connector")]
    [PluginName("XmlRpcGroups")]
    public partial class FlotsamGroupsConnector : GroupsServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("FLOTSAM GROUPS CONNECTOR");

        private AvatarNameServiceInterface m_AvatarNameService;
        private readonly string m_AvatarNameServiceNames;
        private readonly string m_ReadKey = string.Empty;
        private readonly string m_WriteKey = string.Empty;
        private readonly string m_Uri;

        public int TimeoutMs { get; set; }

        public FlotsamGroupsConnector(IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationErrorException();
            }
            m_Uri = ownSection.GetString("URI");

            TimeoutMs = 20000;
            m_AvatarNameServiceNames = ownSection.GetString("AvatarNameServices", "AvatarNameStorage").Trim();
        }

        public void Startup(ConfigurationLoader loader)
        {
            var list = new RwLockedList<AvatarNameServiceInterface>();
            foreach(string name in m_AvatarNameServiceNames.Split(','))
            {
                list.Add(loader.GetService<AvatarNameServiceInterface>(name.Trim()));
            }
            m_AvatarNameService = new AggregatingAvatarNameService(list);
        }

        protected IValue FlotsamXmlRpcCall(UUI requestingAgent, string methodName, Map structparam)
        {
            var req = new XmlRpc.XmlRpcRequest
            {
                MethodName = methodName
            };
            structparam.Add("RequestingAgentID", requestingAgent.ID);
            structparam.Add("RequestingAgentUserService", requestingAgent.HomeURI);
            structparam.Add("RequestingSessionID", UUID.Zero);
            structparam.Add("ReadKey", m_ReadKey);
            structparam.Add("WriteKey", m_WriteKey);
            req.Params.Add(structparam);
            XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);
            if (!(res.ReturnValue is Map))
            {
                throw new InvalidDataException("Unexpected FlotsamGroups return value");
            }
            var p = (Map)res.ReturnValue;
            if (!p.ContainsKey("success"))
            {
                throw new InvalidDataException("Unexpected FlotsamGroups return value");
            }

            if (p["success"].ToString().ToLower() != "true")
            {
                throw new KeyNotFoundException();
            }
            return (p.ContainsKey("results")) ? p["results"] : null /* some calls have no data */;
        }

        protected IValue FlotsamXmlRpcGetCall(UUI requestingAgent, string methodName, Map structparam)
        {
            var req = new XmlRpc.XmlRpcRequest
            {
                MethodName = methodName
            };
            structparam.Add("RequestingAgentID", requestingAgent.ID);
            structparam.Add("RequestingAgentUserService", requestingAgent.HomeURI);
            structparam.Add("RequestingSessionID", UUID.Zero);
            structparam.Add("ReadKey", m_ReadKey);
            structparam.Add("WriteKey", m_WriteKey);
            req.Params.Add(structparam);
            XmlRpc.XmlRpcResponse res = RPC.DoXmlRpcRequest(m_Uri, req, TimeoutMs);
            var p = res.ReturnValue as Map;
            if (p != null && p.ContainsKey("error"))
            {
                throw new InvalidDataException("Unexpected FlotsamGroups return value");
            }

            return res.ReturnValue;
        }

        public override IGroupsInterface Groups => this;

        public override IGroupRolesInterface Roles => this;

        public override IGroupMembersInterface Members => this;

        public override IGroupMembershipsInterface Memberships => this;

        public override IGroupRolemembersInterface Rolemembers => this;

        public override IGroupSelectInterface ActiveGroup => this;

        public override IActiveGroupMembershipInterface ActiveMembership => this;

        public override IGroupInvitesInterface Invites => this;

        public override IGroupNoticesInterface Notices => this;
    }
}
