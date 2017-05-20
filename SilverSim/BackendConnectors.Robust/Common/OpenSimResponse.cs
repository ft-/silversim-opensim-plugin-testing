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
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace SilverSim.BackendConnectors.Robust.Common
{
    public static class OpenSimResponse
    {
        [Serializable]
        public class InvalidOpenSimResponseSerializationException : Exception
        {
            public string Path = string.Empty;

            public InvalidOpenSimResponseSerializationException(string path)
                : base(path)
            {
                Path = path;
            }

            public InvalidOpenSimResponseSerializationException()
            {
            }

            public InvalidOpenSimResponseSerializationException(string msg, Exception innerException) : base(msg, innerException)
            {
            }

            protected InvalidOpenSimResponseSerializationException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }

            public override string Message => "Invalid OpenSim response at " + Path;
        }

        private static AString ParseValue(XmlTextReader reader)
        {
            var astring = new AString();
            string tagname = reader.Name;
            while(true)
            {
                if(!reader.Read())
                {
                    throw new InvalidOpenSimResponseSerializationException("/" + tagname);
                }

                switch(reader.NodeType)
                {
                    case XmlNodeType.Element:
                        throw new InvalidOpenSimResponseSerializationException("/" + tagname);

                    case XmlNodeType.Text:
                        return new AString(reader.ReadContentAsString());

                    case XmlNodeType.EndElement:
                        if(reader.Name != tagname)
                        {
                            throw new InvalidOpenSimResponseSerializationException("/" + tagname);
                        }
                        return astring;

                    default:
                        break;
                }
            }
        }

        private static Map ParseMap(XmlTextReader reader)
        {
            string tagname = reader.Name;
            var map = new Map();
            while(true)
            {
                if (!reader.Read())
                {
                    throw new InvalidOpenSimResponseSerializationException("/" + tagname);
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
                                    map[reader.Name] = ParseMap(reader);
                                }
                                catch(InvalidOpenSimResponseSerializationException e)
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
                                map[reader.Name] = ParseValue(reader);
                            }
                            catch (InvalidOpenSimResponseSerializationException e)
                            {
                                e.Path = "/" + tagname + e.Path;
                                throw;
                            }
                        }
                        break;

                    case XmlNodeType.EndElement:
                        if (reader.Name != tagname)
                        {
                            throw new InvalidOpenSimResponseSerializationException("/" + tagname);
                        }

                        return map;

                    default:
                        break;
                }
            }
        }

        public static Map Deserialize(Stream input)
        {
            using(var reader = new XmlTextReader(input))
            {
                while(true)
                {
                    if(!reader.Read())
                    {
                        throw new InvalidOpenSimResponseSerializationException("/");
                    }

                    if(reader.NodeType == XmlNodeType.Element)
                    {
                        if(reader.Name != "ServerResponse")
                        {
                            throw new InvalidOpenSimResponseSerializationException("/");
                        }
                        else if(reader.IsEmptyElement)
                        {
                            return new Map();
                        }
                        else
                        {
                            try
                            {
                                return ParseMap(reader);
                            }
                            catch (InvalidOpenSimResponseSerializationException e)
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
