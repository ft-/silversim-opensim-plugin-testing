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
using SilverSim.ServiceInterfaces.MuteList;
using SilverSim.Types;
using SilverSim.Types.MuteList;
using SilverSim.Types.StructuredData.REST;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.MuteList
{
    [Description("Robust Friends Protocol Server")]
    [PluginName("MuteListHandler")]
    public sealed class MuteListServerHandler : IPlugin
    {
        private BaseHttpServer m_HttpServer;
        private readonly string m_MuteListServiceName;
        private MuteListServiceInterface m_MuteListService;
        private readonly Dictionary<string, Action<HttpRequest, Dictionary<string, object>>> m_Handlers = new Dictionary<string, Action<HttpRequest, Dictionary<string, object>>>();

        public MuteListServerHandler(IConfig config)
        {
            m_MuteListServiceName = config.GetString("MuteListService");
            m_Handlers.Add("get", HandleGetMuteList);
            m_Handlers.Add("update", HandleUpdateMuteListEntry);
            m_Handlers.Add("delete", HandleDeleteMuteListEntry);
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_HttpServer = loader.HttpServer;
            m_MuteListService = loader.GetService<MuteListServiceInterface>(m_MuteListServiceName);

            m_HttpServer.UriHandlers.Add("/mutelist", MuteListHandler);
            BaseHttpServer https;
            if (loader.TryGetHttpsServer(out https))
            {
                https.UriHandlers.Add("/mutelist", MuteListHandler);
            }
        }

        private void MuteListHandler(HttpRequest httpreq)
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

            Action<HttpRequest, Dictionary<string, object>> del;
            try
            {
                if (m_Handlers.TryGetValue(reqdata["METHOD"].ToString(), out del))
                {
                    del(httpreq, reqdata);
                }
                else
                {
                    throw new FailureResultException();
                }
            }
            catch (FailureResultException e)
            {
                using (HttpResponse res = httpreq.BeginResponse("text/xml"))
                {
                    using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
                    {
                        writer.WriteStartElement("ServerResponse");
                        writer.WriteNamedValue("Result", "Failure");
                        writer.WriteNamedValue("Message", e.Message);
                        writer.WriteEndElement();
                    }
                }
            }
            catch
            {
                if (httpreq.Response != null)
                {
                    httpreq.Response.Close();
                }
                else
                {
                    FailureResult(httpreq);
                }
            }
        }

        private void HandleGetMuteList(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            object agentid_str;
            object mutecrc_str;
            uint mutecrc;
            UUID agentid;
            if(!reqdata.TryGetValue("agentid", out agentid_str) ||
                !reqdata.TryGetValue("mutecrc", out mutecrc_str) ||
                !UUID.TryParse(agentid_str.ToString(), out agentid) ||
                !uint.TryParse(mutecrc_str.ToString(), out mutecrc))
            {
                FailureResult(httpreq);
                return;
            }

            List<MuteListEntry> list;
            string b64list;
            try
            {
                byte[] data;
                list = m_MuteListService.GetList(agentid, mutecrc);
                data = list.ToBinaryData();
                b64list = (list.Count != 0 && new Crc32().Compute(data) == mutecrc) ?
                    Convert.ToBase64String(new byte[1] { 1 }) :
                    Convert.ToBase64String(list.ToBinaryData());
            }
            catch (UseCachedMuteListException)
            {
                b64list = Convert.ToBase64String(new byte[1] { 1 }); /* UseCachedMuteList signal in OpenSim protocol defs */
            }
            catch
            {
                FailureResult(httpreq);
                return;
            }

            using (HttpResponse res = httpreq.BeginResponse("text/xml"))
            using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteNamedValue("result", b64list);
                writer.WriteEndElement();
            }

        }

        private void HandleUpdateMuteListEntry(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID muteid;
            UUID agentid;
            object o;
            if (!reqdata.TryGetValue("agentid", out o) ||
                !UUID.TryParse(o.ToString(), out agentid) ||
                !reqdata.TryGetValue("muteid", out o) ||
                !UUID.TryParse(o.ToString(), out muteid))
            {
                FailureResult(httpreq);
                return;
            }
            var e = new MuteListEntry
            {
                MuteID = muteid,
                Flags = MuteFlags.None,
                Type = MuteType.ByName
            };
            if(reqdata.TryGetValue("mutename", out o))
            {
                e.MuteName = o.ToString();
            }
            int intval;
            if(reqdata.TryGetValue("mutetype", out o) && int.TryParse(o.ToString(), out intval))
            {
                e.Type = (MuteType)intval;
            }
            uint uintval;
            if (reqdata.TryGetValue("muteflags", out o) && uint.TryParse(o.ToString(), out uintval))
            {
                e.Flags = (MuteFlags)uintval;
            }
            try
            {
                m_MuteListService.Store(agentid, e);
            }
            catch
            {
                FailureResult(httpreq);
                return;
            }
            SuccessResult(httpreq);
        }

        private void HandleDeleteMuteListEntry(HttpRequest httpreq, Dictionary<string, object> reqdata)
        {
            UUID muteid;
            UUID agentid;
            object o;
            if (!reqdata.TryGetValue("agentid", out o) ||
                !UUID.TryParse(o.ToString(), out agentid) ||
                !reqdata.TryGetValue("muteid", out o) ||
                !UUID.TryParse(o.ToString(), out muteid))
            {
                FailureResult(httpreq);
                return;
            }
            string mutename = string.Empty;
            if(reqdata.TryGetValue("mutename", out o))
            {
                mutename = o.ToString();
            }

            try
            {
                m_MuteListService.Remove(agentid, muteid, mutename);
            }
            catch
            {
                FailureResult(httpreq);
                return;
            }
            SuccessResult(httpreq);
        }

        private void SuccessResult(HttpRequest req)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteNamedValue("result", "Success");
                writer.WriteEndElement();
            }
        }

        private void FailureResult(HttpRequest req)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            using (XmlTextWriter writer = res.GetOutputStream().UTF8XmlTextWriter())
            {
                writer.WriteStartElement("ServerResponse");
                writer.WriteNamedValue("result", "Failure");
                writer.WriteEndElement();
            }
        }
    }
}
