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
using SilverSim.Types.Parcel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace SilverSim.OpenSimArchiver.RegionArchiver
{
    public static partial class OAR
    {
        private static class ParcelLoader
        {
            private static void LoadParcelAccessListEntry(XmlTextReader reader, List<ParcelAccessEntry> whiteList, List<ParcelAccessEntry> blackList)
            {
                var pae = new ParcelAccessEntry();
                OarAccessFlags flags = 0;

                for (;;)
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
                                case "AgentID":
                                    pae.Accessor.ID = UUID.Parse(reader.ReadElementValueAsString());
                                    break;

                                case "AgentData":
                                    pae.Accessor.CreatorData = reader.ReadElementValueAsString();
                                    break;

                                case "Expires":
                                    pae.ExpiresAt = Date.UnixTimeToDateTime(reader.ReadElementValueAsULong());
                                    break;

                                case "AccessList":
                                    flags = (OarAccessFlags)reader.ReadElementValueAsUInt();
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
                            if (reader.Name != "ParcelAccessEntry")
                            {
                                throw new OARFormatException();
                            }
                            if((flags & OarAccessFlags.Access) != 0)
                            {
                                whiteList.Add(pae);
                            }
                            if((flags & OarAccessFlags.Ban) != 0)
                            {
                                blackList.Add(pae);
                            }
                            return;

                        default:
                            break;
                    }
                }
            }

            private static void LoadParcelAccessList(XmlTextReader reader, List<ParcelAccessEntry> whiteList, List<ParcelAccessEntry> blackList)
            {
                if(reader.IsEmptyElement)
                {
                    return;
                }
                for (;;)
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
                                case "ParcelAccessEntry":
                                    if(reader.IsEmptyElement)
                                    {
                                        break;
                                    }
                                    LoadParcelAccessListEntry(reader, whiteList, blackList);
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
                            if (reader.Name != "ParcelAccessList")
                            {
                                throw new OARFormatException();
                            }
                            return;

                        default:
                            break;
                    }
                }
            }

            private static void LoadParcelInner(XmlTextReader reader, ParcelInfo pinfo, List<ParcelAccessEntry> whiteList, List<ParcelAccessEntry> blackList)
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
                                case "Area":
                                    pinfo.Area = reader.ReadElementValueAsInt();
                                    break;

                                case "AuctionID":
                                    pinfo.AuctionID = reader.ReadElementValueAsUInt();
                                    break;

                                case "AuthBuyerID":
                                    pinfo.AuthBuyer = new UUI(reader.ReadElementValueAsString());
                                    break;

                                case "Category":
                                    int parcelCat = reader.ReadElementValueAsInt();
                                    pinfo.Category = (ParcelCategory)Math.Max(parcelCat, 0);
                                    break;

                                case "ClaimDate":
                                    pinfo.ClaimDate = reader.ReadElementValueAsCrazyDate();
                                    break;

                                case "ClaimPrice":
                                    pinfo.ClaimPrice = reader.ReadElementValueAsInt();
                                    break;

                                case "GlobalID":
                                    pinfo.ID = reader.ReadElementValueAsString();
                                    break;

                                case "GroupID":
                                    pinfo.Group.ID = reader.ReadElementValueAsString();
                                    break;

                                case "IsGroupOwned":
                                    pinfo.GroupOwned = reader.ReadElementValueAsBoolean();
                                    break;

                                case "Bitmap":
                                    pinfo.LandBitmap.Data = Convert.FromBase64String(reader.ReadElementValueAsString());
                                    break;

                                case "Description":
                                    pinfo.Description = reader.ReadElementValueAsString();
                                    break;

                                case "Flags":
                                    pinfo.Flags = (ParcelFlags)reader.ReadElementValueAsUInt();
                                    break;

                                case "LandingType":
                                    pinfo.LandingType = (TeleportLandingType)reader.ReadElementValueAsUInt();
                                    break;

                                case "Name":
                                    pinfo.Name = reader.ReadElementValueAsString();
                                    break;

                                case "Status":
                                    pinfo.Status = (ParcelStatus)reader.ReadElementValueAsUInt();
                                    break;

                                case "LocalID":
                                    pinfo.LocalID = reader.ReadElementValueAsInt();
                                    break;

                                case "MediaAutoScale":
                                    pinfo.MediaAutoScale = reader.ReadElementValueAsUInt() != 0;
                                    break;

                                case "MediaID":
                                    pinfo.MediaID = reader.ReadElementValueAsString();
                                    break;

                                case "MediaURL":
                                    {
                                        string url = reader.ReadElementValueAsString();
                                        if(!string.IsNullOrEmpty(url))
                                        {
                                            pinfo.MediaURI = new URI(url);
                                        }
                                    }
                                    break;

                                case "MusicURL":
                                    {
                                        string url = reader.ReadElementValueAsString();
                                        if(!string.IsNullOrEmpty(url))
                                        {
                                            pinfo.MusicURI = new URI(url);
                                        }
                                    }
                                    break;

                                case "OwnerID":
                                    pinfo.Owner.ID = reader.ReadElementValueAsString();
                                    break;

                                case "ParcelAccessList":
                                    LoadParcelAccessList(reader, whiteList, blackList);
                                    break;

                                case "PassHours":
                                    pinfo.PassHours = reader.ReadElementValueAsDouble();
                                    break;

                                case "PassPrice":
                                    pinfo.PassPrice = reader.ReadElementValueAsInt();
                                    break;

                                case "SalePrice":
                                    pinfo.SalePrice = reader.ReadElementValueAsInt();
                                    break;

                                case "SnapshotID":
                                    pinfo.SnapshotID = reader.ReadElementValueAsString();
                                    break;

                                case "UserLocation":
                                    if(!Vector3.TryParse(reader.ReadElementValueAsString(), out pinfo.LandingPosition))
                                    {
                                        throw new OARFormatException();
                                    }
                                    break;

                                case "UserLookAt":
                                    if(!Vector3.TryParse(reader.ReadElementValueAsString(), out pinfo.LandingLookAt))
                                    {
                                        throw new OARFormatException();
                                    }
                                    break;

                                case "Dwell":
                                    pinfo.Dwell = reader.ReadElementValueAsDouble();
                                    break;

                                case "OtherCleanTime":
                                    pinfo.OtherCleanTime = reader.ReadElementValueAsInt();
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
                            if(reader.Name != "LandData")
                            {
                                throw new OARFormatException();
                            }
                            return;

                        default:
                            break;
                    }
                }
            }

            private static ParcelInfo LoadParcel(XmlTextReader reader, GridVector regionSize, List<ParcelAccessEntry> whiteList, List<ParcelAccessEntry> blackList)
            {
                var pinfo = new ParcelInfo((int)regionSize.X / 4, (int)regionSize.Y / 4);
                for(;;)
                {
                    if(!reader.Read())
                    {
                        throw new OARFormatException();
                    }

                    switch(reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if(reader.Name != "LandData")
                            {
                                throw new OARFormatException();
                            }
                            LoadParcelInner(reader, pinfo, whiteList, blackList);
                            return pinfo;

                        default:
                            break;
                    }
                }
            }

            public static ParcelInfo GetParcelInfo(Stream s, GridVector regionSize, List<ParcelAccessEntry> whiteList, List<ParcelAccessEntry> blackList)
            {
                using(var reader = new XmlTextReader(s))
                {
                    return LoadParcel(reader, regionSize, whiteList, blackList);
                }
            }
        }
    }
}
