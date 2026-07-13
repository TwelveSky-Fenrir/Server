namespace Fenrir.Core.Opcodes;

public static class Opcodes
{
    public static class Login
    {
        public static class Incoming
        {
            public const byte Loggedin = 11;
            public const byte LoginKeepAlive = 12;
            public const byte CreateMousePin = 13;
            public const byte ChangeMousePin = 14;
            public const byte VerifyMousePin = 15;
            public const byte CreateAvatar = 17;
            public const byte DeleteAvatar = 18;
            public const byte RenameAvatar = 19;
            public const byte ClaimGift = 21;
            public const byte ZoneTransfer = 22;
            public const byte ZoneTransferFailure = 23;
            public const byte ChangeMaster = 24;
            public const byte GiftList = 25;
        }

        public static class Outgoing
        {
            public const byte LoginGreeting = 0;
            public const byte Loggedin = 11;
            public const byte AvatarRoster = 12;
            public const byte CreateMousePin = 13;
            public const byte ChangeMousePin = 14;
            public const byte VerifyMousePin = 15;
            public const byte CreateAvatar = 17;
            public const byte DeleteAvatar = 18;
            public const byte RenameAvatar = 19;
            public const byte ClaimGift = 21;
            public const byte ZoneTransfer = 22;
            public const byte WorldRecommendation = 24;
            public const byte GiftList = 25;
            public const byte WorldRecommendationFinal = 26;
        }
    }

    public static class Zone
    {
        public static class Incoming
        {
            public const byte ZoneHandshake = 11;
            public const byte EnterWorld = 12;
            public const byte ZoneReady = 13;
            public const byte AvatarAction = 15;
            public const byte AvatarActionResume = 16;
            public const byte GlobalAnnouncement = 17;
            public const byte Attack = 18;
            public const byte GenericAction = 19;
            public const byte ZoneMove = 20;
            public const byte ZoneTransferCancel = 21;
            public const byte UseHotkeyItem = 22;
            public const byte UseInventoryItem = 23;
            public const byte EnchantItem = 24;
            public const byte CombineItem = 25;
            public const byte RerollItem = 26;
            public const byte UpgradeItemRank = 27;
            public const byte DowngradeItemRank = 28;
            public const byte CraftItem = 29;
            public const byte CraftSkillBook = 30;
            public const byte OpenShopStall = 31;
            public const byte CloseShopStall = 32;
            public const byte ViewShopStall = 33;
            public const byte SearchShopListings = 34;
            public const byte BuyShopItem = 35;
            public const byte QuestProgress = 36;
            public const byte TribeMigration = 37;
            public const byte LocalChat = 38;
            public const byte Whisper = 39;
            public const byte Shout = 40;
            public const byte GetCashBalance = 41;
            public const byte BuyCashItem = 42;
            public const byte DuelChallenge = 43;
            public const byte DuelCancel = 44;
            public const byte DuelAnswer = 45;
            public const byte DuelStart = 46;
            public const byte TradeInvite = 47;
            public const byte TradeCancel = 48;
            public const byte TradeAnswer = 49;
            public const byte TradeStart = 50;
            public const byte TradeLock = 51;
            public const byte TradeEnd = 52;
            public const byte Friend = 53;
            public const byte FriendCancel = 54;
            public const byte FriendAnswer = 55;
            public const byte FriendAdd = 56;
            public const byte FriendLocate = 57;
            public const byte FriendRemove = 58;
            public const byte Mentor = 59;
            public const byte MentorCancel = 60;
            public const byte MentorAnswer = 61;
            public const byte MentorStart = 62;
            public const byte MentorEnd = 63;
            public const byte MentorStatus = 64;
            public const byte PartyInvite = 65;
            public const byte PartyCancel = 66;
            public const byte PartyAnswer = 67;
            public const byte PartyChat = 68;
            public const byte PartyLeave = 69;
            public const byte PartyKick = 70;
            public const byte PartyDisband = 71;
            public const byte GuildInvite = 72;
            public const byte GuildInviteCancel = 73;
            public const byte GuildInviteAnswer = 74;
            public const byte GuildAction = 75;
            public const byte GuildAnnouncement = 76;
            public const byte GuildChat = 77;
            public const byte FindGuildMember = 78;
            public const byte TribeAction = 79;
            public const byte TribeAnnouncement = 80;
            public const byte TribeChat = 81;
            public const byte TribeBank = 82;
            public const byte TribeVote = 83;
            public const byte AutoPotionThreshold = 86;
            public const byte MountState = 87;
            public const byte CraftPet = 88;
            public const byte DestroyItem = 89;
            public const byte CostumeState = 90;
            public const byte GetCashCatalog = 91;
            public const byte TribePopulation = 92;
            public const byte SkyUpgradeItem = 93;
            public const byte ContinueSkillStat = 94;
            public const byte ContinueSkillUse = 95;
            public const byte DiceBattle = 96;
            public const byte PlaytimeBuff = 97;
            public const byte SocketSystem = 98;

            public const byte AutoHuntToggle = 99;
            public const byte ZoneWar297TypeCancel = 100;
            public const byte LoginEvent1 = 101;
            public const byte SmeltItem = 102;
            public const byte FishingLine = 103;
            public const byte FishingProgress = 104;
            public const byte FishingCatch = 105;
            public const byte TrapCheck = 106;
            public const byte DecideChallengeFourGuild = 107;
            public const byte GetProxyShop = 108;
            public const byte UpdateProxyShop = 109;
            public const byte WithdrawProxyShopEarnings = 110;
            public const byte RankBuff = 111;
            public const byte TribeAnnouncementScroll = 112;
            public const byte MountAbsorb = 113;
            public const byte CapsuleItemBuy = 114;
            public const byte PatAction = 115;
            public const byte ServiceMove = 116;
            public const byte RageBuff = 117;
            public const byte HeroRanking = 118;
            public const byte HeroRewardClaim = 119;
            public const byte TowerUpgrade = 120;
            public const byte SocketSlotInsert = 121;

            public const byte DailyMission = 126;
            public const byte UpgradeCape = 127;
            public const byte MixSkillUse = 128;
            public const byte DrinkBottle = 129;
            public const byte CraftLegendaryPet = 131;
            public const byte AddAbility = 132;
            public const byte MakeItem133 = 133;
            public const byte MakeItem134 = 134;
            public const byte MakeItem135 = 135;
            public const byte PcRoomPet = 136;
            public const byte MakeItem137 = 137;
            public const byte MakeItem138 = 138;
            public const byte CostumeVisibility = 139;
            public const byte GetBloodMarkCatalog = 140;
            public const byte BuyBloodMarkItem = 141;
            public const byte RegisterTournament = 150;
            public const byte Heartbeat = 151;
            public const byte WorldChat = 152;
            public const byte StellarCoreState = 153;
            public const byte GetDailyRewardCatalog = 154;
            public const byte ClaimDailyReward = 155;
            public const byte PetActionUpdate = 156;
            public const byte RuneSocket = 157;
        }

        public static class Outgoing
        {
            public const byte ZoneGreeting = 0;
            public const byte ZoneHandshake = 11;
            public const byte EnterWorld = 12;
            public const byte WorldSnapshot = 13;
            public const byte AvatarAction = 15;
            public const byte AvatarEffectState = 16;
            public const byte AvatarStateFlag = 17;
            public const byte MonsterReplication = 18;
            public const byte GroundItemReplication = 19;
            public const byte GlobalAnnouncement = 20;
            public const byte Attack = 21;
            public const byte AvatarStatUpdate = 22;
            public const byte GenericAction = 23;
            public const byte ZoneMove = 24;
            public const byte UseHotkeyItem = 25;
            public const byte UseInventoryItem = 26;
            public const byte EnchantItem = 27;
            public const byte CombineItem = 28;
            public const byte RerollItem = 29;
            public const byte UpgradeItemRank = 30;
            public const byte DowngradeItemRank = 31;
            public const byte CraftItem = 32;
            public const byte CraftSkillBook = 33;
            public const byte OpenShopStall = 34;
            public const byte CloseShopStall = 35;
            public const byte ViewShopStall = 36;
            public const byte SearchShopListings = 37;
            public const byte BuyShopItem = 38;
            public const byte QuestProgress = 39;
            public const byte TribeMigration = 40;
            public const byte LocalChat = 41;
            public const byte Whisper = 42;
            public const byte Shout = 43;
            public const byte GetCashBalance = 44;
            public const byte BuyCashItem = 45;
            public const byte DuelChallenge = 46;
            public const byte DuelCancel = 47;
            public const byte DuelAnswer = 48;
            public const byte DuelStart = 49;
            public const byte DuelCountdown = 50;
            public const byte DuelEnd = 51;
            public const byte TradeInvite = 52;
            public const byte TradeCancel = 53;
            public const byte TradeAnswer = 54;
            public const byte TradeStart = 55;
            public const byte TradeUpdate = 56;
            public const byte TradeLock = 57;
            public const byte TradeEnd = 58;
            public const byte Friend = 59;
            public const byte FriendCancel = 60;
            public const byte FriendAnswer = 61;
            public const byte FriendAdd = 62;
            public const byte FriendLocate = 63;
            public const byte FriendRemove = 64;
            public const byte Mentor = 65;
            public const byte MentorCancel = 66;
            public const byte MentorAnswer = 67;
            public const byte MentorStart = 68;
            public const byte MentorEnd = 69;
            public const byte MentorStatus = 70;
            public const byte PartyInvite = 71;
            public const byte PartyCancel = 72;
            public const byte PartyAnswer = 73;
            public const byte PartyRoster = 74;
            public const byte PartyMemberJoined = 75;
            public const byte PartyChat = 76;
            public const byte PartyLeave = 77;
            public const byte PartyKick = 78;
            public const byte PartyDisband = 79;
            public const byte GuildInvite = 80;
            public const byte GuildInviteCancel = 81;
            public const byte GuildInviteAnswer = 82;
            public const byte GuildAction = 83;
            public const byte GuildAnnouncement = 84;
            public const byte GuildChat = 85;
            public const byte FindGuildMember = 86;
            public const byte GuildMemberConnected = 87;
            public const byte TribeAction = 88;
            public const byte TribeAnnouncement = 89;
            public const byte TribeChat = 90;
            public const byte TribeBank = 91;
            public const byte TribeVote = 92;
            public const byte TribeAllianceStatus = 93;
            public const byte ZoneEventInfo = 94;
            public const byte ZoneWar049Status = 95;
            public const byte ZoneWar051Status = 96;
            public const byte GmCommand = 97;
            public const byte ReturnToHomeZone = 98;
            public const byte ZoneWar194Status = 100;
            public const byte ZoneWar194Countdown = 101;
            public const byte MountState = 102;
            public const byte ZoneWar267Status = 103;
            public const byte ZoneWar241Status = 104;
            public const byte CraftPet = 105;
            public const byte DestroyItem = 106;
            public const byte CostumeState = 108;
            public const byte GetCashCatalog = 109;
            public const byte CashCatalogInvalidated = 110;
            public const byte TribePopulation = 111;
            public const byte SkyUpgradeItem = 112;
            public const byte AutoBuffRegister = 113;
            public const byte AutoBuffActivation = 114;
            public const byte ZoneWar297Status = 116;
            public const byte ZoneWar297MonsterCount = 118;
            public const byte MultiItemCreate = 119;
            public const byte AutoHuntHotkeyRebind = 120;
            public const byte AutoHuntToggle = 123;
            public const byte FishingLine = 127;
            public const byte FishingProgress = 128;
            public const byte FishingCatch = 129;
            public const byte ProxyShopStallState = 134;
            public const byte GetProxyShop = 135;
            public const byte UpdateProxyShop = 136;
            public const byte WithdrawProxyShopEarnings = 137;
            public const byte AddInventoryItem = 138;
            public const byte TribeAnnouncementScroll = 139;
            public const byte HeroRankingPrevious = 148;
            public const byte HeroRewardClaim = 149;
            public const byte HeroRankingCurrent = 150;
            public const byte TowerUpgrade = 151;
            public const byte TowerStatus = 152;
            public const byte TowerRepairInfo = 153;
            public const byte DailyMission = 163;
            public const byte UpgradeCape = 164;
            public const byte DrunkState = 165;
            public const byte CraftLegendaryPet = 168;
            public const byte CostumeVisibility = 177;
            public const byte GetBloodMarkCatalog = 178;
            public const byte BuyBloodMarkItem = 179;
            public const byte WorldChat = 192;
            public const byte StellarCoreState = 193;
            public const byte SetInventorySlot = 194;
            public const byte GetDailyRewardCatalog = 195;
            public const byte ClaimDailyReward = 196;
            public const byte RuneSocket = 199;
            public const byte ZoneWar335Countdown = 200;
        }
    }

        public static class Center
    {
        public static class Incoming
        {
            public const byte WorldEvent = 33;
        }

        public static class Outgoing
        {
            public const byte WorldEvent = 33;
        }
    }
}
