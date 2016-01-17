// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace SilverSim.OpenSimArchiver.RegionArchiver
{
    public static partial class OAR
    {
        public static class ArchiveXmlLoader
        {
            public class RegionInfo
            {
                public UUID ID = UUID.Zero;
                public Date CreationDate = new Date();
                public string Path = string.Empty;
                public bool IsMegaregion;
                public GridVector Location = new GridVector(0, 0);
                public GridVector RegionSize = new GridVector(256, 256);
                
                public RegionInfo()
                {

                }
            }

            #region Major Version 0
            static void LoadArchiveXmlVersion0_CreationInfo(XmlTextReader reader, RegionInfo rinfo)
            {
                for(;;)
                {
                    if(!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch(reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch(reader.Name)
                            {
                                case "datetime":
                                    rinfo.CreationDate = Date.UnixTimeToDateTime(reader.ReadElementValueAsULong());
                                    break;
                                    
                                case "id":
                                    rinfo.ID = reader.ReadElementValueAsString();
                                    break;

                                default:
                                    if(!reader.IsEmptyElement)
                                    {
                                        reader.Skip();
                                    }
                                    break;
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if(reader.Name != "creation_info")
                            {
                                throw new OARFormatException();
                            }
                            return;

                        default:
                            break;
                    }
                }
            }

            static void LoadArchiveXmlVersion0_RegionInfo(XmlTextReader reader, RegionInfo rinfo)
            {
                for (; ; )
                {
                    if (!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch (reader.Name)
                            {
                                case "is_megaregion":
                                    rinfo.IsMegaregion = reader.ReadElementValueAsBoolean();
                                    break;

                                case "size_in_meters":
                                    rinfo.RegionSize = new GridVector(reader.ReadElementValueAsString());
                                    break;

                                default:
                                    if (!reader.IsEmptyElement)
                                    {
                                        reader.Skip();
                                    }
                                    break;
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if (reader.Name != "region_info")
                            {
                                throw new OARFormatException();
                            }
                            return;

                        default:
                            break;
                    }
                }
            }

            static RegionInfo LoadArchiveXmlVersion0(XmlTextReader reader)
            {
                RegionInfo rinfo = new RegionInfo();
                for(;;)
                {
                    if(!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch(reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch(reader.Name)
                            {
                                case "creation_info":
                                    if (reader.IsEmptyElement)
                                    {
                                        throw new OARFormatException();
                                    }
                                    LoadArchiveXmlVersion0_CreationInfo(reader, rinfo);
                                    break;

                                case "region_info":
                                    if (reader.IsEmptyElement)
                                    {
                                        throw new OARFormatException();
                                    }
                                    LoadArchiveXmlVersion0_RegionInfo(reader, rinfo);
                                    break;

                                default:
                                    if(!reader.IsEmptyElement)
                                    {
                                        reader.Skip();
                                    }
                                    break;
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if(reader.Name != "archive")
                            {
                                throw new OARFormatException();
                            }
                            return rinfo;

                        default:
                            break;
                    }
                }
            }
            #endregion

            #region Major Version 1
            static Date LoadArchiveXmlVersion1_CreationInfo(XmlTextReader reader)
            {
                Date creationDate = new Date();
                for (; ; )
                {
                    if (!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch (reader.Name)
                            {
                                case "datetime":
                                    creationDate = Date.UnixTimeToDateTime(reader.ReadElementValueAsULong());
                                    break;

                                default:
                                    if (!reader.IsEmptyElement)
                                    {
                                        reader.Skip();
                                    }
                                    break;
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if (reader.Name != "creation_info")
                            {
                                throw new OARFormatException();
                            }
                            return creationDate;

                        default:
                            break;
                    }
                }
            }

            static GridVector LoadArchiveXmlVersion1_Region(XmlTextReader reader, List<RegionInfo> regionInfos, GridVector loc)
            {
                RegionInfo rinfo = new RegionInfo();
                rinfo.Location = loc;
                for (; ; )
                {
                    if (!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch (reader.Name)
                            {
                                case "id":
                                    rinfo.ID = reader.ReadElementValueAsString();
                                    break;

                                case "dir":
                                    rinfo.Path = reader.ReadElementValueAsString();
                                    break;

                                case "is_megaregion":
                                    rinfo.IsMegaregion = reader.ReadElementValueAsBoolean();
                                    break;

                                case "size_in_meters":
                                    rinfo.RegionSize = new GridVector(reader.ReadElementValueAsString());
                                    break;

                                default:
                                    if (!reader.IsEmptyElement)
                                    {
                                        reader.Skip();
                                    }
                                    break;
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if (reader.Name != "region")
                            {
                                throw new OARFormatException();
                            }
                            regionInfos.Add(rinfo);
                            return rinfo.RegionSize;

                        default:
                            break;
                    }
                }
            }

            static void LoadArchiveXmlVersion1_Row(XmlTextReader reader, List<RegionInfo> regionInfos, GridVector loc)
            {
                for (; ; )
                {
                    if (!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch (reader.Name)
                            {
                                case "region":
                                    if (reader.IsEmptyElement)
                                    {
                                        throw new OARFormatException();
                                    }
                                    loc.X += LoadArchiveXmlVersion1_Region(reader, regionInfos, loc).X;
                                    break;

                                default:
                                    if (!reader.IsEmptyElement)
                                    {
                                        reader.Skip();
                                    }
                                    break;
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if (reader.Name != "row")
                            {
                                throw new OARFormatException();
                            }
                            return;

                        default:
                            break;
                    }
                }
            }

            static void LoadArchiveXmlVersion1_Regions(XmlTextReader reader, List<RegionInfo> regionInfos)
            {
                GridVector loc = new GridVector(0, 0);
                for (; ; )
                {
                    if(!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch(reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch(reader.Name)
                            {
                                case "row":
                                    if(reader.IsEmptyElement)
                                    {
                                        throw new OARFormatException();
                                    }
                                    LoadArchiveXmlVersion1_Row(reader, regionInfos, loc);
                                    loc.Y += 256;
                                    break;

                                default:
                                    if(!reader.IsEmptyElement)
                                    {
                                        reader.Skip();
                                    }
                                    break;
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if(reader.Name != "regions")
                            {
                                throw new OARFormatException();
                            }
                            return;

                        default:
                            break;
                    }
                }
            }

            static RegionInfo LoadArchiveXmlVersion1(XmlTextReader reader, List<RegionInfo> regionInfos)
            {
                for (; ; )
                {
                    if (!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch (reader.Name)
                            {
                                case "creation_info":
                                    if (reader.IsEmptyElement)
                                    {
                                        throw new OARFormatException();
                                    }
                                    LoadArchiveXmlVersion1_CreationInfo(reader);
                                    break;

                                case "regions":
                                    if (!reader.IsEmptyElement)
                                    {
                                        LoadArchiveXmlVersion1_Regions(reader, regionInfos);
                                    }
                                    break;

                                default:
                                    if (!reader.IsEmptyElement)
                                    {
                                        reader.Skip();
                                    }
                                    break;
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if (reader.Name != "archive")
                            {
                                throw new OARFormatException();
                            }
                            return regionInfos[0];

                        default:
                            break;
                    }
                }
            }
            #endregion

            static RegionInfo LoadArchiveXml(
                XmlTextReader reader,
                List<RegionInfo> regionInfos)
            {
                uint majorVersion = 0;
                uint minorVersion = 0;

                for (; ;)
                {
                    if(!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch(reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if(reader.IsEmptyElement)
                            {
                                throw new OARFormatException();
                            }
                            if(reader.Name != "archive")
                            {
                                throw new OARFormatException();
                            }

                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    switch (reader.Name)
                                    {
                                        case "major_version":
                                            if(!uint.TryParse(reader.Value, out majorVersion))
                                            {
                                                throw new OARFormatException();
                                            }
                                            break;

                                        case "minor_version":
                                            if(!uint.TryParse(reader.Value, out minorVersion))
                                            {
                                                throw new OARFormatException();
                                            }
                                            break;

                                        default:
                                            break;
                                    }
                                }
                                while (reader.MoveToNextAttribute());
                            }

                            if(majorVersion == 0 && minorVersion != 0)
                            {
                                return LoadArchiveXmlVersion0(reader);
                            }
                            else if(majorVersion == 1)
                            {
                                return LoadArchiveXmlVersion1(reader, regionInfos);
                            }
                            else
                            {
                                throw new OARFormatException();
                            }

                        default:
                            break;
                    }
                }
            }

            public static RegionInfo LoadArchiveXml(
                Stream s, 
                List<RegionInfo> regionInfos)
            {
                using (XmlTextReader reader = new XmlTextReader(s))
                {
                    return LoadArchiveXml(reader, regionInfos);
                }
            }
        }
    }
}
