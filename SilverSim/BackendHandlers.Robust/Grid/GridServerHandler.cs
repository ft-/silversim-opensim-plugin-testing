﻿// SilverSim is distributed under the terms of the
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
using SilverSim.Main.Common.HttpServer;
using SilverSim.ServiceInterfaces.Grid;
using SilverSim.ServiceInterfaces.ServerParam;
using SilverSim.Types;
using SilverSim.Types.Grid;
using SilverSim.Types.StructuredData.REST;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Xml;

namespace SilverSim.BackendHandlers.Robust.Grid
{
    #region Service Implementation
    [Description("Robust Grid Protocol Server")]
    [ServerParam("GridName", ParameterType = typeof(Uri), Type = ServerParamType.GlobalOnly, DefaultValue = "")]
    [PluginName("GridHandler")]
    public sealed class RobustGridServerHandler : IPlugin, IServerParamListener
    {
        private readonly string m_GridServiceName;
        private GridServiceInterface m_GridService;
        private readonly Dictionary<string, object> m_ExtraFeatures = new Dictionary<string, object>();

        private static readonly ILog m_Log = LogManager.GetLogger("ROBUST GRID HANDLER");
        public RobustGridServerHandler(IConfig ownSection)
        {
            m_GridServiceName = ownSection.GetString("GridService", "GridService");
            m_ExtraFeatures["ExportSupported"] = "true";
            m_ExtraFeatures["gridname"] = string.Empty;
        }

        public void Startup(ConfigurationLoader loader)
        {
            m_GridService = loader.GetService<GridServiceInterface>(m_GridServiceName);
            loader.HttpServer.UriHandlers.Add("/grid", GridAccessHandler);
            try
            {
                loader.HttpsServer.UriHandlers.Add("/grid", GridAccessHandler);
            }
            catch
            {
                /* intentionally left empty */
            }
        }

        [ServerParam("GridName")]
        public void HandleGridName(UUID regionid, string value)
        {
            if (regionid != UUID.Zero)
            {
                return;
            }
            m_ExtraFeatures["gridname"] = value;
        }

        private void SuccessResult(HttpRequest req, RegionInfo rInfo)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (Stream o = res.GetOutputStream())
                {
                    using (XmlTextWriter w = o.UTF8XmlTextWriter())
                    {
                        w.WriteStartElement("ServerResponse");
                        w.WriteStartElement("result");
                        w.WriteAttributeString("type", "List");
                        SerializeRegion(w, rInfo);
                        w.WriteEndElement();
                        w.WriteEndElement();
                    }
                }
            }
        }

        private void SuccessResult(HttpRequest req, List<RegionInfo> regions)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (Stream o = res.GetOutputStream())
                {
                    using (XmlTextWriter w = o.UTF8XmlTextWriter())
                    {
                        w.WriteStartElement("ServerResponse");
                        w.WriteStartElement("result");
                        if (regions.Count == 0)
                        {
                            w.WriteValue("null");
                        }
                        else
                        {
                            w.WriteAttributeString("type", "List");
                            SerializeRegionList(w, regions);
                        }
                        w.WriteEndElement();
                        w.WriteEndElement();
                    }
                }
            }
        }

        private void ListFailureResult(HttpRequest req)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (Stream o = res.GetOutputStream())
                {
                    using (XmlTextWriter w = o.UTF8XmlTextWriter())
                    {
                        w.WriteStartElement("ServerResponse");
                        w.WriteStartElement("result");
                        w.WriteValue("null");
                        w.WriteEndElement();
                        w.WriteEndElement();
                    }
                }
            }
        }

        private void FailureResult(HttpRequest req, string message)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (Stream o = res.GetOutputStream())
                {
                    using (XmlWriter w = o.UTF8XmlTextWriter())
                    {
                        w.WriteStartElement("ServerResponse");
                        w.WriteStartElement("Result");
                        w.WriteValue("Failure");
                        w.WriteEndElement();
                        w.WriteStartElement("Message");
                        w.WriteValue(message);
                        w.WriteEndElement();
                        w.WriteEndElement();
                    }
                }
            }
        }

        private void SuccessResult(HttpRequest req)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (Stream o = res.GetOutputStream())
                {
                    using (XmlWriter w = o.UTF8XmlTextWriter())
                    {
                        w.WriteStartElement("ServerResponse");
                        w.WriteStartElement("Result");
                        w.WriteValue("Success");
                        w.WriteEndElement();
                        w.WriteEndElement();
                    }
                }
            }
        }

        private void GridAccessHandler(HttpRequest req)
        {
            if (req.ContainsHeader("X-SecondLife-Shard"))
            {
                req.ErrorResponse(HttpStatusCode.BadRequest, "Request source not allowed");
                return;
            }

            if (req.Method != "POST")
            {
                req.ErrorResponse(HttpStatusCode.MethodNotAllowed);
                return;
            }

            Dictionary<string, object> reqdata;
            using (Stream s = req.Body)
            {
                try
                {
                    reqdata = REST.ParseREST(s);
                }
                catch
                {
                    req.ErrorResponse(HttpStatusCode.BadRequest, "Bad request");
                    return;
                }
            }

            if(!reqdata.ContainsKey("METHOD"))
            {
                FailureResult(req, "METHOD missing");
                return;
            }

            switch(reqdata["METHOD"].ToString())
            {
                case "register":
                    RegisterRegion(req, reqdata);
                    break;

                case "deregister":
                    DeregisterRegion(req, reqdata);
                    break;

                case "get_neighbours":
                    GetNeighbours(req, reqdata);
                    break;

                case "get_region_by_uuid":
                    GetRegionById(req, reqdata);
                    break;

                case "get_region_by_position":
                    GetRegionByPosition(req, reqdata);
                    break;

                case "get_region_by_name":
                    GetRegionByName(req, reqdata);
                    break;

                case "get_region_name":
                    GetRegionsByName(req, reqdata);
                    break;

                case "get_region_range":
                    GetRegionRange(req, reqdata);
                    break;

                case "get_default_regions":
                    GetDefaultRegions(req, reqdata);
                    break;

                case "get_default_hypergrid_regions":
                    GetDefaultHypergridRegions(req, reqdata);
                    break;

                case "get_fallback_regions":
                    GetFallbackRegions(req, reqdata);
                    break;

                case "get_hyperlinks":
                    GetHyperlinks(req, reqdata);
                    break;

                case "get_region_flags":
                    GetRegionFlags(req, reqdata);
                    break;

                case "get_grid_extra_features":
                    GetGridExtraFeatures(req, reqdata);
                    break;

                default:
                    FailureResult(req, "Unknown method");
                    break;
            }
        }

        private void RegisterRegion(HttpRequest req, Dictionary<string, object> reqdata)
        {
            if (!reqdata.ContainsKey("uuid") ||
                !reqdata.ContainsKey("regionName"))
            {
                FailureResult(req, "Missing parameters");
                return;
            }

            var rInfo = new RegionInfo();

            if (!UUID.TryParse(reqdata["uuid"].ToString(), out rInfo.ID))
            {
                FailureResult(req, "Invalid parameter uuid");
                return;
            }
            if(rInfo.ID == UUID.Zero)
            {
                FailureResult(req, "Special region not allowed to be registered");
                return;
            }

            rInfo.Name = reqdata["regionName"].ToString();

            if (reqdata.ContainsKey("SCOPEID") &&
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out rInfo.ScopeID))
            {
                FailureResult(req, "Invalid parameter SCOPEID");
                return;
            }

            if (reqdata.ContainsKey("owner_uuid") &&
                !UUID.TryParse(reqdata["owner_uuid"].ToString(), out rInfo.Owner.ID))
            {
                FailureResult(req, "Invalid parameter owner_uuid");
                return;
            }

            if (reqdata.ContainsKey("owner_data"))
            {
                rInfo.Owner.CreatorData = reqdata["owner_data"].ToString();
            }

            if (!reqdata.ContainsKey("locX") ||
                !uint.TryParse(reqdata["locX"].ToString(), out rInfo.Location.X))
            {
                FailureResult(req, "Invalid parameter locX");
                return;
            }

            if (!reqdata.ContainsKey("locY") ||
                !uint.TryParse(reqdata["locY"].ToString(), out rInfo.Location.Y))
            {
                FailureResult(req, "Invalid parameter locY");
                return;
            }

            rInfo.Size.X = 256;
            if(reqdata.ContainsKey("sizeX") &&
                !uint.TryParse(reqdata["sizeX"].ToString(),out rInfo.Size.X))
            {
                FailureResult(req, "Invalid parameter sizeX");
                return;
            }

            rInfo.Size.Y = 256;
            if (reqdata.ContainsKey("sizeY") &&
                !uint.TryParse(reqdata["sizeY"].ToString(), out rInfo.Size.Y))
            {
                FailureResult(req, "Invalid parameter sizeY");
                return;
            }

            if(!reqdata.ContainsKey("serverIP"))
            {
                FailureResult(req, "Missing parameter serverIP");
                return;
            }
            rInfo.ServerIP = reqdata["serverIP"].ToString();

            if(!reqdata.ContainsKey("serverURI"))
            {
                FailureResult(req, "Missing parameter serverURI");
                return;
            }
            rInfo.ServerURI = reqdata["serverURI"].ToString();

            if(!reqdata.ContainsKey("serverHttpPort") ||
                uint.TryParse(reqdata["serverHttpPort"].ToString(), out rInfo.ServerHttpPort) ||
                rInfo.ServerHttpPort < 1 || rInfo.ServerHttpPort > 65535)
            {
                FailureResult(req, "Invalid parameter serverHttpPort");
                return;
            }

            if (!reqdata.ContainsKey("serverPort") ||
                uint.TryParse(reqdata["serverPort"].ToString(), out rInfo.ServerPort) ||
                rInfo.ServerPort < 1 || rInfo.ServerPort > 65535)
            {
                FailureResult(req, "Invalid parameter serverPort");
                return;
            }

            if(reqdata.ContainsKey("regionMapTexture") &&
                !UUID.TryParse(reqdata["regionMapTexture"].ToString(), out rInfo.RegionMapTexture))
            {
                FailureResult(req, "Invalid parameter regionMapTexture");
                return;
            }

            if (reqdata.ContainsKey("parcelMapTexture") &&
                !UUID.TryParse(reqdata["parcelMapTexture"].ToString(), out rInfo.ParcelMapTexture))
            {
                FailureResult(req, "Invalid parameter parcelMapTexture");
                return;
            }

            var access_val = (uint)RegionAccess.Adult;
            if(reqdata.ContainsKey("access") &&
                !uint.TryParse(reqdata["access"].ToString(), out access_val))
            {
                FailureResult(req, "Invalid parameter access");
                return;
            }
            rInfo.Access = (RegionAccess)access_val;

            if(reqdata.ContainsKey("PrincipalID") &&
                !UUI.TryParse(reqdata["PrincipalID"].ToString(),out rInfo.AuthenticatingPrincipal))
            {
                FailureResult(req, "Invalid parameter PrincipalID");
                return;
            }

            if(reqdata.ContainsKey("Token"))
            {
                rInfo.AuthenticatingToken = reqdata["Token"].ToString();
            }

            uint flags_val;
            rInfo.Flags = (reqdata.ContainsKey("flags") &&
                uint.TryParse(reqdata["flags"].ToString(), out flags_val)) ?
                (RegionFlags)flags_val :
                RegionFlags.RegionOnline;

            try
            {
                m_GridService.RegisterRegion(rInfo);
            }
            catch(Exception e)
            {
                FailureResult(req, e.Message);
#if DEBUG
                m_Log.ErrorFormat("Failed to register region {0} ({1}): {2}\n{3}",
                    rInfo.Name, rInfo.ID.ToString(),
                    e.Message,
                    e.StackTrace);
#else
                m_Log.ErrorFormat("Failed to register region {0} ({1}): {2}",
                    rInfo.Name, rInfo.ID.ToString(),
                    e.Message);
#endif
            }
        }

        private void DeregisterRegion(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID regionid;
            if(!reqdata.ContainsKey("REGIONID") ||
                !UUID.TryParse(reqdata["REGIONID"].ToString(),out regionid))
            {
                FailureResult(req, "Invalid parameter REGIONID");
                return;
            }
            UUID scopeid;
            if(!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            try
            {
                m_GridService.UnregisterRegion(scopeid, regionid);
            }
            catch(Exception e)
            {
                FailureResult(req, e.Message);
                return;
            }
            SuccessResult(req);
        }

        private void SerializeRegionData(XmlTextWriter w, RegionInfo rInfo)
        {
            w.WriteNamedValue("uuid", rInfo.ID);
            w.WriteNamedValue("locX", rInfo.Location.X);
            w.WriteNamedValue("locY", rInfo.Location.Y);
            w.WriteNamedValue("sizeX", rInfo.Size.X);
            w.WriteNamedValue("sizeY", rInfo.Size.Y);
            w.WriteNamedValue("regionName", rInfo.Name);
            w.WriteNamedValue("flags", (uint)rInfo.Flags);
            w.WriteNamedValue("serverIP", rInfo.ServerIP);
            w.WriteNamedValue("serverHttpPort", rInfo.ServerHttpPort);
            w.WriteNamedValue("serverURI", rInfo.ServerURI);
            w.WriteNamedValue("serverPort", rInfo.ServerPort);
            w.WriteNamedValue("regionMapTexture", rInfo.RegionMapTexture);
            w.WriteNamedValue("parcelMapTexture", rInfo.ParcelMapTexture);
            w.WriteNamedValue("access", (uint)rInfo.Access);
            w.WriteNamedValue("regionSecret", rInfo.RegionSecret);
            w.WriteNamedValue("owner_uuid", rInfo.Owner.ID);
            w.WriteNamedValue("owner_data", rInfo.Owner.CreatorData);
        }

        private void SerializeRegion(XmlTextWriter w, RegionInfo rInfo)
        {
            w.WriteStartElement("region");
            w.WriteAttributeString("type", "List");
            SerializeRegionData(w, rInfo);
            w.WriteEndElement();
        }

        private void SerializeRegionList(XmlTextWriter w, List<RegionInfo> list)
        {
            int cnt = 1;
            foreach(RegionInfo rInfo in list)
            {
                w.WriteStartElement("region" + (cnt++).ToString());
                w.WriteAttributeString("type", "List");
                SerializeRegionData(w, rInfo);
                w.WriteEndElement();
            }
        }

        private void GetNeighbours(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID regionid;
            if (!reqdata.ContainsKey("REGIONID") ||
                !UUID.TryParse(reqdata["REGIONID"].ToString(), out regionid))
            {
                ListFailureResult(req);
                return;
            }
            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            List<RegionInfo> regions;
            try
            {
                regions = m_GridService.GetNeighbours(scopeid, regionid);
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, regions);
        }

        private void GetRegionById(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID regionid;
            if (!reqdata.ContainsKey("REGIONID") ||
                !UUID.TryParse(reqdata["REGIONID"].ToString(), out regionid))
            {
                ListFailureResult(req);
                return;
            }
            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            RegionInfo rInfo;
            try
            {
                rInfo = m_GridService[scopeid, regionid];
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, rInfo);
        }

        private void GetRegionByPosition(HttpRequest req, Dictionary<string, object> reqdata)
        {
            uint x;
            uint y;
            if (!reqdata.ContainsKey("X") ||
                !uint.TryParse(reqdata["X"].ToString(), out x))
            {
                ListFailureResult(req);
                return;
            }
            if (!reqdata.ContainsKey("Y") ||
                !uint.TryParse(reqdata["Y"].ToString(), out y))
            {
                ListFailureResult(req);
                return;
            }
            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            RegionInfo rInfo;
            try
            {
                rInfo = m_GridService[scopeid, x, y];
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, rInfo);
        }

        private void GetRegionByName(HttpRequest req, Dictionary<string, object> reqdata)
        {
            string name;
            if (!reqdata.ContainsKey("NAME"))
            {
                ListFailureResult(req);
                return;
            }
            name = reqdata["NAME"].ToString();
            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            RegionInfo rInfo;
            try
            {
                rInfo = m_GridService[scopeid, name];
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, rInfo);
        }

        private void GetRegionsByName(HttpRequest req, Dictionary<string, object> reqdata)
        {
            string name;
            if (!reqdata.ContainsKey("NAME"))
            {
                ListFailureResult(req);
                return;
            }
            name = reqdata["NAME"].ToString();

            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            List<RegionInfo> regions;
            try
            {
                regions = m_GridService.SearchRegionsByName(scopeid, name);
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, regions);
        }

        private void GetRegionRange(HttpRequest req, Dictionary<string, object> reqdata)
        {
            var min = new GridVector();
            var max = new GridVector();

            if (!reqdata.ContainsKey("XMIN") ||
                uint.TryParse(reqdata["XMIN"].ToString(), out min.X) ||
                !reqdata.ContainsKey("YMIN") ||
                uint.TryParse(reqdata["YMIN"].ToString(), out min.Y) ||
                !reqdata.ContainsKey("XMAX") ||
                uint.TryParse(reqdata["XMAX"].ToString(), out max.X) ||
                !reqdata.ContainsKey("YMAX") ||
                uint.TryParse(reqdata["YMAX"].ToString(), out max.Y))
            {
                ListFailureResult(req);
                return;
            }

            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            List<RegionInfo> regions;
            try
            {
                regions = m_GridService.GetRegionsByRange(scopeid, min, max);
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, regions);
        }

        private void GetDefaultRegions(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            List<RegionInfo> regions;
            try
            {
                regions = m_GridService.GetDefaultRegions(scopeid);
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, regions);
        }

        private void GetDefaultHypergridRegions(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            List<RegionInfo> regions;
            try
            {
                regions = m_GridService.GetDefaultHypergridRegions(scopeid);
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, regions);
        }

        private void GetFallbackRegions(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            List<RegionInfo> regions;
            try
            {
                regions = m_GridService.GetFallbackRegions(scopeid);
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, regions);
        }

        private void GetHyperlinks(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            List<RegionInfo> regions;
            try
            {
                regions = m_GridService.GetHyperlinks(scopeid);
            }
            catch
            {
                ListFailureResult(req);
                return;
            }
            SuccessResult(req, regions);
        }

        private void GetRegionFlags(HttpRequest req, Dictionary<string, object> reqdata)
        {
            UUID regionid;
            if (!reqdata.ContainsKey("REGIONID") ||
                !UUID.TryParse(reqdata["REGIONID"].ToString(), out regionid))
            {
                ListFailureResult(req);
                return;
            }
            UUID scopeid;
            if (!reqdata.ContainsKey("SCOPEID") ||
                !UUID.TryParse(reqdata["SCOPEID"].ToString(), out scopeid))
            {
                scopeid = UUID.Zero;
            }

            RegionInfo rInfo;
            uint regionFlags;
            try
            {
                rInfo = m_GridService[scopeid, regionid];
                regionFlags = (uint)rInfo.Flags;
            }
            catch
            {
                regionFlags = 0;
            }
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (Stream o = res.GetOutputStream())
                {
                    using (XmlTextWriter w = o.UTF8XmlTextWriter())
                    {
                        w.WriteStartElement("ServerResponse");
                        w.WriteNamedValue("result", regionFlags);
                        w.WriteEndElement();
                    }
                }
            }
        }

        private void GetGridExtraFeatures(HttpRequest req, Dictionary<string, object> reqdata)
        {
            using (HttpResponse res = req.BeginResponse("text/xml"))
            {
                using (Stream o = res.GetOutputStream())
                {
                    using (XmlWriter w = o.UTF8XmlTextWriter())
                    {
                        w.WriteStartElement("ServerResponse");
                        List<Dictionary<string, object>.Enumerator> enumStack = new List<Dictionary<string, object>.Enumerator>();
                        enumStack.Insert(0, m_ExtraFeatures.GetEnumerator());
                        w.WriteStartElement("ServerResponse");
                        while(enumStack.Count != 0)
                        {
                            Dictionary<string, object>.Enumerator enumerator = enumStack[0];
                            if(enumerator.MoveNext())
                            {
                                KeyValuePair<string, object> kvp = enumerator.Current;
                                w.WriteStartElement(kvp.Key);
                                object obj = kvp.Value;
                                var dict = obj as Dictionary<string, object>;
                                if(dict != null)
                                {
                                    w.WriteAttributeString("type", "List");
                                    enumStack.Insert(0, dict.GetEnumerator());
                                }
                                else
                                {
                                    w.WriteValue(obj.ToString());
                                    w.WriteEndElement();
                                }
                            }
                            else
                            {
                                w.WriteEndElement();
                                enumStack.RemoveAt(0);
                            }
                        }
                    }
                }
            }
        }
    }
    #endregion
}
