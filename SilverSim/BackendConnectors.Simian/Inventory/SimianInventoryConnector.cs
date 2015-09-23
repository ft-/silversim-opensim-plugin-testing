// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using log4net;
using Nini.Config;
using SilverSim.BackendConnectors.Simian.Common;
using SilverSim.Main.Common;
using SilverSim.ServiceInterfaces.Groups;
using SilverSim.ServiceInterfaces.Inventory;
using SilverSim.Types;
using SilverSim.Types.Asset;
using SilverSim.Types.Inventory;
using System.Collections.Generic;

namespace SilverSim.BackendConnectors.Simian.Inventory
{
    #region Service Implementation
    public class SimianInventoryConnector : InventoryServiceInterface, IPlugin
    {
        private static readonly ILog m_Log = LogManager.GetLogger("SIMIAN INVENTORY");

        private string m_InventoryURI;
        private SimianInventoryFolderConnector m_FolderService;
        private SimianInventoryItemConnector m_ItemService;
        private GroupsServiceInterface m_GroupsService;
        private int m_TimeoutMs = 20000;
        private string m_InventoryCapability;

        #region Constructor
        public SimianInventoryConnector(string uri, string simCapability)
        {
            if(!uri.EndsWith("/") && !uri.EndsWith("="))
            {
                uri += "/";
            }
            m_InventoryURI = uri;
            m_ItemService = new SimianInventoryItemConnector(uri, null, simCapability);
            m_ItemService.TimeoutMs = m_TimeoutMs;
            m_FolderService = new SimianInventoryFolderConnector(uri, null, simCapability);
            m_FolderService.TimeoutMs = m_TimeoutMs;
            m_InventoryCapability = simCapability;
        }

        public SimianInventoryConnector(string uri, GroupsServiceInterface groupsService, string simCapability)
        {
            m_InventoryCapability = simCapability;
            m_GroupsService = groupsService;
            if (!uri.EndsWith("/") && !uri.EndsWith("="))
            {
                uri += "/";
            }
            m_InventoryURI = uri;
            m_ItemService = new SimianInventoryItemConnector(uri, m_GroupsService, simCapability);
            m_ItemService.TimeoutMs = m_TimeoutMs;
            m_FolderService = new SimianInventoryFolderConnector(uri, m_GroupsService, simCapability);
            m_FolderService.TimeoutMs = m_TimeoutMs;
        }

        public void Startup(ConfigurationLoader loader)
        {

        }
        #endregion

        #region Accessors
        public int TimeoutMs
        {
            get
            {
                return m_TimeoutMs;
            }
            set
            {
                m_TimeoutMs = value;
                m_FolderService.TimeoutMs = value;
                m_ItemService.TimeoutMs = value;
            }
        }

        public override InventoryFolderServiceInterface Folder
        {
            get
            {
                return m_FolderService;
            }
        }

        public override InventoryItemServiceInterface Item
        {
            get
            {
                return m_ItemService;
            }
        }

        public override List<InventoryItem> getActiveGestures(UUID PrincipalID)
        {
            List<InventoryItem> item = new List<InventoryItem>();
            Dictionary<string, string> post = new Dictionary<string, string>();
            post["RequestMethod"] = "GetUser";
            post["UserID"] = (string)PrincipalID;

            Map res = SimianGrid.PostToService(m_InventoryURI, m_InventoryCapability, post, TimeoutMs);
            if (res["Success"].AsBoolean && res.ContainsKey("Gestures") && res["Gestures"] is AnArray)
            {
                AnArray gestures = (AnArray)res["Gestures"];
                foreach(IValue v in gestures)
                {
                    try
                    {
                        item.Add(Item[PrincipalID, v.AsUUID]);
                    }
                    catch
                    {

                    }
                }
            }
            throw new InventoryInaccessible();
        }
        #endregion

        #region Map converson
        internal static string ContentTypeFromAssetType(AssetType type)
        {
            switch(type)
            {
                case AssetType.Unknown: return "application/octet-stream";
                case AssetType.Texture: return "image/x-j2c";
                case AssetType.TextureTGA: return "image/tga";
                case AssetType.ImageJPEG: return "image/jpeg";
                case AssetType.ImageTGA: return "image/tga";
                case AssetType.Sound: return "audio/ogg";
                case AssetType.SoundWAV: return "audio/x-wav";
                case AssetType.CallingCard: return "application/vnd.ll.callingcard";
                case AssetType.Landmark: return "application/vnd.ll.landmark";
                case AssetType.Clothing: return "application/vnd.ll.clothing";
                case AssetType.Object: return "application/vnd.ll.primitive";
                case AssetType.Notecard: return "application/vnd.ll.notecard";
                //case AssetType.Folder: return "application/vnd.ll.folder";
                case AssetType.RootFolder: return "application/vnd.ll.rootfolder";
                case AssetType.LSLText: return "application/vnd.ll.lsltext";
                case AssetType.LSLBytecode: return "application/vnd.ll.lslbyte";
                case AssetType.Bodypart: return "application/vnd.ll.bodypart";
                case AssetType.TrashFolder: return "application/vnd.ll.trashfolder";
                case AssetType.SnapshotFolder: return "application/vnd.ll.snapshotfolder";
                case AssetType.LostAndFoundFolder: return "application/vnd.ll.lostandfoundfolder";
                case AssetType.Animation: return "application/vnd.ll.animation";
                case AssetType.Gesture: return "application/vnd.ll.gesture";
                case AssetType.Simstate: return "application/x-metaverse-simstate";
                case AssetType.FavoriteFolder: return "application/vnd.ll.favoritefolder";
                case AssetType.Link: return "application/vnd.ll.link";
                case AssetType.LinkFolder: return "application/vnd.ll.linkfolder";
                case AssetType.CurrentOutfitFolder: return "application/vnd.ll.currentoutfitfolder";
                case AssetType.OutfitFolder: return "application/vnd.ll.outfitfolder";
                case AssetType.MyOutfitsFolder: return "application/vnd.ll.myoutfitsfolder";
                case AssetType.Mesh: return "application/vnd.ll.mesh";
                case AssetType.Material: return "application/llsd+xml";
                default: return "application/octet-stream";
            }
        }

        internal static AssetType AssetTypeFromContentType(string contenttype)
        {
            switch(contenttype)
            {
                case "application/octet-stream": return AssetType.Unknown;
                case "image/x-j2c": return AssetType.Texture;
                case "image/jp2": return AssetType.Texture;
                case "image/tga": return AssetType.TextureTGA;
                case "image/jpeg": return AssetType.ImageJPEG;
                case "audio/ogg": return AssetType.Sound;
                case "audio/x-wav": return AssetType.SoundWAV;

                case "application/vnd.ll.callingcard": return AssetType.CallingCard;
                case "application/x-metaverse-callingcard": return AssetType.CallingCard;

                case "application/vnd.ll.landmark": return AssetType.Landmark;
                case "application/x-metaverse-landmark": return AssetType.Landmark;

                case "application/vnd.ll.clothing": return AssetType.Clothing;
                case "application/x-metaverse-clothing": return AssetType.Clothing;

                case "application/vnd.ll.primitive": return AssetType.Object;
                case "application/x-metaverse-primitive": return AssetType.Object;

                case "application/vnd.ll.notecard": return AssetType.Notecard;
                case "application/x-metaverse-notecard": return AssetType.Notecard;

                //case "application/vnd.ll.folder": return AssetType.Folder;
                case "application/vnd.ll.rootfolder": return AssetType.RootFolder;

                case "application/vnd.ll.lsltext": return AssetType.LSLText;
                case "application/x-metaverse-lsl": return AssetType.LSLText;

                case "application/vnd.ll.lslbyte": return AssetType.LSLBytecode;
                case "application/x-metaverse-lso": return AssetType.LSLBytecode;

                case "application/vnd.ll.bodypart": return AssetType.Bodypart;
                case "application/x-metaverse-bodypart": return AssetType.Bodypart;

                case "application/vnd.ll.trashfolder": return AssetType.TrashFolder;
                case "application/vnd.ll.snapshotfolder": return AssetType.SnapshotFolder;
                case "application/vnd.ll.lostandfoundfolder": return AssetType.LostAndFoundFolder;

                case "application/vnd.ll.animation": return AssetType.Animation;
                case "application/x-metaverse-animation": return AssetType.Animation;

                case "application/vnd.ll.gesture": return AssetType.Gesture;
                case "application/x-metaverse-gesture": return AssetType.Gesture;

                case "application/x-metaverse-simstate": return AssetType.Simstate;

                case "application/vnd.ll.favoritefolder": return AssetType.FavoriteFolder;

                case "application/vnd.ll.link": return AssetType.Link;

                case "application/vnd.ll.linkfolder": return AssetType.LinkFolder;
                    
                case "application/vnd.ll.currentoutfitfolder": return AssetType.CurrentOutfitFolder;

                case "application/vnd.ll.outfitfolder": return AssetType.OutfitFolder;

                case "application/vnd.ll.myoutfitsfolder": return AssetType.MyOutfitsFolder;

                case "application/vnd.ll.mesh": return AssetType.Mesh;

                case "application/llsd+xml": return AssetType.Material;

                default: return AssetType.Unknown;
            }
        }

        internal static string ContentTypeFromInventoryType(InventoryType type)
        {
            switch(type)
            {
                case InventoryType.Unknown: return "application/octet-stream";
                case InventoryType.Texture: return "image/x-j2c";
                case InventoryType.TextureTGA: return "image/tga";
                case InventoryType.Sound: return "audio/ogg";
                case InventoryType.CallingCard: return "application/vnd.ll.callingcard";
                case InventoryType.Landmark: return "application/vnd.ll.landmark";
                case InventoryType.Clothing: return "application/vnd.ll.clothing";
                case InventoryType.Object: return "application/vnd.ll.primitive";
                case InventoryType.Notecard: return "application/vnd.ll.notecard";
                case InventoryType.Folder: return "application/vnd.ll.folder";
                case InventoryType.RootFolder: return "application/vnd.ll.rootfolder";
                case InventoryType.LSLText: return "application/vnd.ll.lsltext";
                case InventoryType.LSLBytecode: return "application/vnd.ll.lslbyte";
                case InventoryType.Bodypart: return "application/vnd.ll.bodypart";
                case InventoryType.TrashFolder: return "application/vnd.ll.trashfolder";
                case InventoryType.SnapshotFolder: return "application/vnd.ll.snapshotfolder";
                case InventoryType.LostAndFoundFolder: return "application/vnd.ll.lostandfoundfolder";
                case InventoryType.Animation: return "application/vnd.ll.animation";
                case InventoryType.Gesture: return "application/vnd.ll.gesture";
                case InventoryType.Simstate: return "application/x-metaverse-simstate";
                case InventoryType.FavoriteFolder: return "application/vnd.ll.favoritefolder";
                case InventoryType.CurrentOutfitFolder: return "application/vnd.ll.currentoutfitfolder";
                case InventoryType.OutfitFolder: return "application/vnd.ll.outfitfolder";
                case InventoryType.MyOutfitsFolder: return "application/vnd.ll.myoutfitsfolder";
                case InventoryType.Mesh: return "application/vnd.ll.mesh";
                default: return "application/octet-stream";
            }
        }

        internal static InventoryType InventoryTypeFromContentType(string contenttype)
        {
            switch (contenttype)
            {
                case "application/octet-stream": return InventoryType.Unknown;
                case "image/x-j2c": return InventoryType.Texture;
                case "image/jp2": return InventoryType.Texture;
                case "image/tga": return InventoryType.TextureTGA;
                case "image/jpeg": return InventoryType.Texture;
                case "audio/ogg": return InventoryType.Sound;
                case "audio/x-wav": return InventoryType.Sound;

                case "application/vnd.ll.callingcard": return InventoryType.CallingCard;
                case "application/x-metaverse-callingcard": return InventoryType.CallingCard;

                case "application/vnd.ll.landmark": return InventoryType.Landmark;
                case "application/x-metaverse-landmark": return InventoryType.Landmark;

                case "application/vnd.ll.clothing": return InventoryType.Clothing;
                case "application/x-metaverse-clothing": return InventoryType.Clothing;

                case "application/vnd.ll.primitive": return InventoryType.Object;
                case "application/x-metaverse-primitive": return InventoryType.Object;

                case "application/vnd.ll.notecard": return InventoryType.Notecard;
                case "application/x-metaverse-notecard": return InventoryType.Notecard;

                case "application/vnd.ll.folder": return InventoryType.Folder;
                case "application/vnd.ll.rootfolder": return InventoryType.RootFolder;

                case "application/vnd.ll.lsltext": return InventoryType.LSLText;
                case "application/x-metaverse-lsl": return InventoryType.LSLText;

                case "application/vnd.ll.lslbyte": return InventoryType.LSLBytecode;
                case "application/x-metaverse-lso": return InventoryType.LSLBytecode;

                case "application/vnd.ll.bodypart": return InventoryType.Bodypart;
                case "application/x-metaverse-bodypart": return InventoryType.Bodypart;

                case "application/vnd.ll.trashfolder": return InventoryType.TrashFolder;
                case "application/vnd.ll.snapshotfolder": return InventoryType.SnapshotFolder;
                case "application/vnd.ll.lostandfoundfolder": return InventoryType.LostAndFoundFolder;

                case "application/vnd.ll.animation": return InventoryType.Animation;
                case "application/x-metaverse-animation": return InventoryType.Animation;

                case "application/vnd.ll.gesture": return InventoryType.Gesture;
                case "application/x-metaverse-gesture": return InventoryType.Gesture;

                case "application/x-metaverse-simstate": return InventoryType.Simstate;

                case "application/vnd.ll.favoritefolder": return InventoryType.FavoriteFolder;

                case "application/vnd.ll.currentoutfitfolder": return InventoryType.CurrentOutfitFolder;

                case "application/vnd.ll.outfitfolder": return InventoryType.OutfitFolder;

                case "application/vnd.ll.myoutfitsfolder": return InventoryType.MyOutfitsFolder;

                case "application/vnd.ll.mesh": return InventoryType.Mesh;

                default: return InventoryType.Unknown;
            }
        }

        internal static InventoryFolder FolderFromMap(Map map)
        {
            InventoryFolder folder = new InventoryFolder();
            folder.ID = map["ID"].AsUUID;
            folder.Owner.ID = map["OwnerID"].AsUUID;
            folder.Name = map["Name"].AsString.ToString();
            folder.Version = map["Version"].AsInteger;
            folder.InventoryType = InventoryTypeFromContentType(map["ContentType"].ToString());
            folder.ParentFolderID = map["ParentID"].AsUUID;
            return folder;
        }

        internal static InventoryItem ItemFromMap(Map map, GroupsServiceInterface groupsService)
        {
            InventoryItem item = new InventoryItem();
            item.AssetID = map["AssetID"].AsUUID;
            item.AssetType = AssetTypeFromContentType(map["ContentType"].ToString());
            item.CreationDate = Date.UnixTimeToDateTime(map["CreationDate"].AsULong);
            if (map["CreatorData"].AsString.ToString() == "")
            {
                item.Creator.ID = map["CreatorID"].AsUUID;
            }
            else
            {
                item.Creator = new UUI(map["CreatorID"].AsUUID, map["CreatorData"].AsString.ToString());
            }
            item.Description = map["Description"].AsString.ToString();
            item.ParentFolderID = map["ParentID"].AsUUID;
            item.ID = map["ID"].AsUUID;
            item.InventoryType = InventoryTypeFromContentType(map["ContentType"].ToString());
            item.Name = map["Name"].AsString.ToString();
            item.Owner.ID = map["OwnerID"].AsUUID;

            Map extraData = map["ExtraData"] as Map;
            if (extraData != null && extraData.Count > 0)
            {
                item.Flags = extraData["Flags"].AsUInt;
                if (groupsService != null)
                {
                    try
                    {
                        item.Group = groupsService.Groups[UUI.Unknown, extraData["GroupID"].AsUUID];
                    }
                    catch
                    {
                        item.Group.ID = extraData["GroupID"].AsUUID;
                    }
                }
                else
                {
                    item.Group.ID = extraData["GroupID"].AsUUID;
                }
                item.IsGroupOwned = extraData["GroupOwned"].AsBoolean;
                item.SaleInfo.Price = extraData["SalePrice"].AsInt;
                item.SaleInfo.Type = (InventoryItem.SaleInfoData.SaleType)extraData["SaleType"].AsUInt;

                Map perms = extraData["Permissions"] as Map;
                if (perms != null)
                {
                    item.Permissions.Base = (InventoryPermissionsMask)perms["BaseMask"].AsUInt;

                    item.Permissions.Current = (InventoryPermissionsMask)perms["OwnerMask"].AsUInt;
                    item.Permissions.EveryOne = (InventoryPermissionsMask)perms["EveryoneMask"].AsUInt;
                    item.Permissions.Group = (InventoryPermissionsMask)perms["GroupMask"].AsUInt;
                    item.Permissions.NextOwner = (InventoryPermissionsMask)perms["NextOwnerMask"].AsUInt;
                }

                if (extraData.ContainsKey("LinkedItemType"))
                {
                    item.AssetType = AssetTypeFromContentType(extraData["LinkedItemType"].ToString());
                }
            }

            if(item.Permissions.Base == InventoryPermissionsMask.None)
            {
                item.Permissions.Base = InventoryPermissionsMask.All;
                item.Permissions.Current = InventoryPermissionsMask.All;
                item.Permissions.EveryOne = InventoryPermissionsMask.All;
                item.Permissions.Group = InventoryPermissionsMask.All;
                item.Permissions.NextOwner = InventoryPermissionsMask.All;
            }
            return item;
        }
        #endregion
    }
    #endregion


    #region Factory
    [PluginName("Inventory")]
    public class SimianInventoryConnectorFactory : IPluginFactory
    {
        private static readonly ILog m_Log = LogManager.GetLogger("SIMIAN INVENTORY CONNECTOR");
        public SimianInventoryConnectorFactory()
        {

        }

        public IPlugin Initialize(ConfigurationLoader loader, IConfig ownSection)
        {
            if (!ownSection.Contains("URI"))
            {
                m_Log.FatalFormat("Missing 'URI' in section {0}", ownSection.Name);
                throw new ConfigurationLoader.ConfigurationError();
            }
            return new SimianInventoryConnector(ownSection.GetString("URI"), ownSection.GetString("SimCapability", (string)UUID.Zero));
        }
    }
    #endregion

}
