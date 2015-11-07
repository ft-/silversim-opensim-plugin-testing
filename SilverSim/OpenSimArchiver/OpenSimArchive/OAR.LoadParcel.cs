// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Types;
using SilverSim.Types.Parcel;
using System;
using System.IO;
using System.Xml;

namespace SilverSim.OpenSimArchiver.RegionArchiver
{
    public static partial class OAR
    {
        static class ParcelLoader
        {
            static void LoadParcelInner(XmlTextReader reader, ParcelInfo pinfo)
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
                                    pinfo.Category = (ParcelCategory)reader.ReadElementValueAsUInt();
                                    break;

                                case "ClaimDate":
                                    pinfo.ClaimDate = Date.UnixTimeToDateTime(reader.ReadElementValueAsULong());
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
                                    if(!reader.IsEmptyElement)
                                    {
#warning support parcel access list loading
                                        reader.Skip();
                                    }
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

            static ParcelInfo LoadParcel(XmlTextReader reader, GridVector regionSize)
            {
                ParcelInfo pinfo = new ParcelInfo((int)regionSize.X / 4, (int)regionSize.Y / 4);
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
                            LoadParcelInner(reader, pinfo);
                            return pinfo;

                        default:
                            break;
                    }
                }
            }

            public static ParcelInfo LoadParcel(Stream s, GridVector regionSize)
            {
                using(XmlTextReader reader = new XmlTextReader(s))
                {
                    return LoadParcel(reader, regionSize);
                }
            }
        }
    }
}
