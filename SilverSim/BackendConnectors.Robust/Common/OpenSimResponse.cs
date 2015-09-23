// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using System;
using System.IO;
using System.Xml;

namespace SilverSim.BackendConnectors.Robust.Common
{
    public static class OpenSimResponse
    {
        public class InvalidOpenSimResponseSerialization : Exception
        {
            public string Path;

            public InvalidOpenSimResponseSerialization(string path)
            {

            }

            public new string Message
            {
                get
                {
                    return "Invalid OpenSim response at " + Path;
                }
            }
        }

        private static AString parseValue(XmlTextReader reader)
        {
            AString astring = new AString();
            string tagname = reader.Name;
            while(true)
            {
                if(!reader.Read())
                {
                    throw new InvalidOpenSimResponseSerialization("/" + tagname);
                }

                switch(reader.NodeType)
                {
                    case XmlNodeType.Element:
                        throw new InvalidOpenSimResponseSerialization("/" + tagname);

                    case XmlNodeType.Text:
                        return new AString(reader.ReadContentAsString());

                    case XmlNodeType.EndElement:
                        if(reader.Name != tagname)
                        {
                            throw new InvalidOpenSimResponseSerialization("/" + tagname);
                        }
                        return astring;
                }
            }
        }

        private static Map parseMap(XmlTextReader reader)
        {
            string tagname = reader.Name;
            Map map = new Map();
            while(true)
            {
                if (!reader.Read())
                {
                    throw new InvalidOpenSimResponseSerialization("/" + tagname);
                }

                switch(reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if(reader.GetAttribute("type") == "List")
                        {
                            if (reader.IsEmptyElement)
                            {
                                map[reader.Name] = new Map();
                            }
                            else
                            {
                                try
                                {
                                    map[reader.Name] = parseMap(reader);
                                }
                                catch(InvalidOpenSimResponseSerialization e)
                                {
                                    e.Path = "/" + tagname + e.Path;
                                    throw;
                                }
                            }
                        }
                        else if(reader.IsEmptyElement)
                        {
                            map[reader.Name] = new AString();
                        }
                        else
                        {
                            try
                            {
                                map[reader.Name] = parseValue(reader);
                            }
                            catch (InvalidOpenSimResponseSerialization e)
                            {
                                e.Path = "/" + tagname + e.Path;
                                throw;
                            }
                        }
                        break;

                    case XmlNodeType.EndElement:
                        if (reader.Name != tagname)
                        {
                            throw new InvalidOpenSimResponseSerialization("/" + tagname);
                        }

                        return map;
                }
            }
        }

        public static Map Deserialize(Stream input)
        {
            using(XmlTextReader reader = new XmlTextReader(input))
            {
                while(true)
                {
                    if(!reader.Read())
                    {
                        throw new InvalidOpenSimResponseSerialization("/");
                    }

                    if(reader.NodeType == XmlNodeType.Element)
                    {
                        if(reader.Name != "ServerResponse")
                        {
                            throw new InvalidOpenSimResponseSerialization("/");
                        }
                        else if(reader.IsEmptyElement)
                        {
                            return new Map();
                        }
                        else
                        {
                            try
                            {
                                return parseMap(reader);
                            }
                            catch (InvalidOpenSimResponseSerialization e)
                            {
                                e.Path = "/ServerResponse" + e.Path;
                                throw;
                            }
                        }
                    }
                }
            }
        }
    }
}
