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

using SilverSim.Types;
using System.IO;
using System.Xml;

namespace SilverSim.OpenSimArchiver.InventoryArchiver
{
    public static partial class IAR
    {
        private static class ArchiveXmlLoader
        {
            private static void LoadArchiveXml(
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
                                            if(!uint.TryParse(reader.Value, out majorVersion))
                                            {
                                                throw new IARFormatException();
                                            }
                                            break;

                                        case "minor_version":
                                            if(!uint.TryParse(reader.Value, out minorVersion))
                                            {
                                                throw new IARFormatException();
                                            }
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
                            else if (majorVersion <= 1)
                            {
                                if(!reader.IsEmptyElement)
                                {
                                    reader.ReadToEndElement("archive");
                                }
                                return;
                            }
                            else
                            {
                                throw new IARFormatException();
                            }

                        default:
                            break;
                    }
                }
            }

            public static void LoadArchiveXml(
                Stream s)
            {
                using (var reader = new XmlTextReader(s))
                {
                    LoadArchiveXml(reader);
                }
            }
        }
    }
}
