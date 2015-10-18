// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using System.IO;
using System.Xml;

namespace SilverSim.OpenSimArchiver.InventoryArchiver
{
    public static partial class IAR
    {
        static class ArchiveXmlLoader
        {
            static void LoadArchiveXml(
                XmlTextReader reader)
            {
                uint majorVersion = 0;
                uint minorVersion = 0;

                for (; ; )
                {
                    if (!reader.Read())
                    {
                        throw new IARFormatException();
                    }

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.IsEmptyElement)
                            {
                                throw new IARFormatException();
                            }
                            if (reader.Name != "archive")
                            {
                                throw new IARFormatException();
                            }

                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    switch (reader.Name)
                                    {
                                        case "major_version":
                                            majorVersion = uint.Parse(reader.Value);
                                            break;

                                        case "minor_version":
                                            minorVersion = uint.Parse(reader.Value);
                                            break;

                                        default:
                                            break;
                                    }
                                }
                                while (reader.MoveToNextAttribute());
                            }

                            if (majorVersion == 0 && minorVersion == 0)
                            {
                                throw new IARFormatException();
                            }
                            else if (majorVersion == 0)
                            {
                                if(!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
                                return;
                            }
                            else
                            {
                                throw new IARFormatException();
                            }
                    }
                }
            }

            public static void LoadArchiveXml(
                Stream s)
            {
                using (XmlTextReader reader = new XmlTextReader(s))
                {
                    LoadArchiveXml(reader);
                }
            }
        }
    }
}
