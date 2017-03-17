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

using Nini.Config;
using SilverSim.Main.Common;
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Account;
using SilverSim.ServiceInterfaces.IM;
using SilverSim.Types;
using SilverSim.Types.IM;
using SilverSim.Types.StructuredData.REST;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.OfflineIM
{
    #region Service Implementation
    [Description("Robust OfflineIM Protocol Server")]
    public sealed class RobustOfflineIMServerHandler : IPlugin
    {
        readonly string m_OfflineIMServiceName;
        OfflineIMServiceInterface m_OfflineIMService;

        readonly string m_UserAccountServiceName;
        UserAccountServiceInterface m_UserAccountService;

        public RobustOfflineIMServerHandler(string offlineIMServiceName, string userAccountServiceName)
        {
            m_OfflineIMServiceName = offlineIMServiceName;
            m_UserAccountServiceName = userAccountServiceName;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_OfflineIMService = loader.GetService<OfflineIMServiceInterface>(m_OfflineIMServiceName);
            m_UserAccountService = loader.GetService<UserAccountServiceInterface>(m_UserAccountServiceName);
            loader.HttpServer.UriHandlers.Add("/offlineim", HandleOfflineIM);
            try
            {
                loader.HttpsServer.UriHandlers.Add("/offlineim", HandleOfflineIM);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        void BoolResult(HttpRequest httpreq, bool success, string reason = "")
        {
            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("RESULT");
                    writer.WriteValue(success);
                    writer.WriteEndElement();
                    if(!success)
                    {
                        writer.WriteStartElement("REASON");
                        writer.WriteValue(reason);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
            }
        }

        void HandleOfflineIM(HttpRequest httpreq)
        {
            if (httpreq.ContainsHeader("X-SecondLife-Shard"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Request source not allowed");
                return;
            }

            if (httpreq.Method != "POST")
            {
                httpreq.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                return;
            }

            Dictionary<string, object> reqdata;
            try
            {
                reqdata = REST.ParseREST(httpreq.Body);
            }
            catch
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest);
                return;
            }

            if (!reqdata.ContainsKey("METHOD"))
            {
                httpreq.ErrorResponse(HttpStatusCode.BadRequest, "Missing 'METHOD' field");
                return;
            }

            switch(reqdata["METHOD"].ToString())
            {
                case "STORE":
                    HandleStoreOfflineIM(httpreq, reqdata);
                    break;

                case "GET":
                    HandleGetOfflineIM(httpreq, reqdata);
                    break;

                default:
                    BoolResult(httpreq, false);
                    break;
            }
        }

        void HandleStoreOfflineIM(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            GridInstantMessage im = new GridInstantMessage();

            if (reqdata.ContainsKey("BinaryBucket"))
            {
                im.BinaryBucket = reqdata["BinaryBucket"].ToString().FromHexStringToByteArray();
            }

            if(reqdata.ContainsKey("Dialog"))
            {
                im.Dialog = (GridInstantMessageDialog)int.Parse(reqdata["Dialog"].ToString());
            }

            if(reqdata.ContainsKey("FromAgentID"))
            {
                UUID agentID;
                if(UUID.TryParse(reqdata["FromAgentID"].ToString(), out agentID))
                {
                    im.FromAgent.ID = agentID;
                    im.FromGroup.ID = agentID;
                }
            }

            if(reqdata.ContainsKey("FromAgentName"))
            {
                im.FromAgent.FullName = reqdata["FromAgentName"].ToString();
            }

            if(reqdata.ContainsKey("FromGroup"))
            {
                bool.TryParse(reqdata["FromGroup"].ToString(), out im.IsFromGroup);
            }

            if(reqdata.ContainsKey("SessionID"))
            {
                UUID.TryParse(reqdata["SessionID"].ToString(), out im.IMSessionID);
            }

            if(reqdata.ContainsKey("Message"))
            {
                im.Message = reqdata["Message"].ToString();
            }

            if(reqdata.ContainsKey("Offline"))
            {
                im.IsOffline = byte.Parse(reqdata["Offline"].ToString()) != 0;
            }

            if(reqdata.ContainsKey("EstateID"))
            {
                im.ParentEstateID = uint.Parse(reqdata["EstateID"].ToString());
            }

            if(reqdata.ContainsKey("Position"))
            {
                Vector3.TryParse(reqdata["Position"].ToString(), out im.Position);
            }

            if(reqdata.ContainsKey("RegionID"))
            {
                UUID.TryParse(reqdata["RegionID"].ToString(), out im.RegionID);
            }

            if(reqdata.ContainsKey("Timestamp"))
            {
                im.Timestamp = Date.UnixTimeToDateTime(ulong.Parse(reqdata["Timestamp"].ToString()));
            }

            if(reqdata.ContainsKey("ToAgentID"))
            {
                UUID.TryParse(reqdata["ToAgentID"].ToString(), out im.ToAgent.ID);
            }

            if (m_UserAccountService.ContainsKey(UUID.Zero, im.ToAgent.ID))
            {
                try
                {
                    m_OfflineIMService.StoreOfflineIM(im);
                }
                catch
                {
                    BoolResult(httpreq, false, "Could not store offline IM");
                    return;
                }
                BoolResult(httpreq, true);
            }
            else
            {
                BoolResult(httpreq, false, "To-Agent not in this grid.");
            }
        }

        void NullResult(HttpRequest httpreq, string reason = "")
        {
            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                {
                    writer.WriteStartElement("ServerResponse");
                    writer.WriteStartElement("RESULT");
                    writer.WriteValue("NULL");
                    writer.WriteEndElement();
                    writer.WriteStartElement("REASON");
                    writer.WriteValue(reason);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }
        }

        void HandleGetOfflineIM(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID principalID;
            if(!reqdata.ContainsKey("PrincipalID") || 
                !UUID.TryParse(reqdata["PrincipalID"].ToString(), out principalID))
            {
                NullResult(httpreq, "Invalid request");
                return;
            }
            List<GridInstantMessage> ims = m_OfflineIMService.GetOfflineIMs(principalID);
            if(ims.Count == 0)
            {
                NullResult(httpreq, "No offline messages");
                return;
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            {
                using (Stream s = res.GetOutputStream())
                {
                    using (XmlTextWriter writer = s.UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteStartElement("RESULT");
                        int n = 1;
                        foreach(GridInstantMessage im in ims)
                        {
                            writer.WriteStartElement("im-" + (n++).ToString());
                            {
                                if(im.BinaryBucket != null)
                                {
                                    writer.WriteNamedValue("BinaryBucket", im.BinaryBucket.ToHexString());
                                }
                                writer.WriteNamedValue("Dialog", ((int)im.Dialog).ToString());
                                if(im.FromGroup != null)
                                {
                                    writer.WriteNamedValue("FromAgentID", im.FromAgent.ID);
                                    writer.WriteNamedValue("FromAgentName", im.FromAgent.FullName);
                                }
                                else
                                {
                                    writer.WriteNamedValue("FromAgentID", im.FromGroup.ID);
                                    writer.WriteNamedValue("FromAgentName", im.FromGroup.FullName);
                                }
                                writer.WriteNamedValue("FromGroup", im.IsFromGroup);
                                writer.WriteNamedValue("SessionID", im.IMSessionID);
                                writer.WriteNamedValue("Message", im.Message);
                                writer.WriteNamedValue("Offline", im.IsOffline ? 1 : 0);
                                writer.WriteNamedValue("EstateID", im.ParentEstateID);
                                writer.WriteNamedValue("Position", im.Position.ToString());
                                writer.WriteNamedValue("RegionID", im.RegionID);
                                writer.WriteNamedValue("Timestamp", im.Timestamp.AsULong);
                                writer.WriteNamedValue("ToAgentID", im.ToAgent.ID);
                            }
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
            }
        }
    }
    #endregion

    #region Factory
    [PluginName("OfflineIMHandler")]
    public sealed class RobustOfflineIMServerHandlerFactory : IPluginFactory
    {
        public RobustOfflineIMServerHandlerFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            return new RobustOfflineIMServerHandler(
                ownSection.GetString("OfflineIMService", "OfflineIMService"),
                ownSection.GetString("UserAccountService", "UserAccountService"));
        }
    }
    #endregion
}
