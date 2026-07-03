namespace Fenrir.Contracts;

/// <summary>
///     Real legacy opcodes (§2.4/§2.5/§4.9/§5.10). Values overlap between <see cref="Login" />/<see cref="Zone" />
///     and between <c>Incoming</c>/<c>Outgoing</c>, hence two nesting axes instead of one flat table.
///     Includes opcodes outside M1 scope (no <c>[FenrirPacket]</c> yet) to avoid ever reusing a value
///     already taken by the real protocol.
/// </summary>
public static class Opcodes
{
    public static class Login
    {
        /// <summary>Client → LoginServer.</summary>
        public static class Incoming
        {
            public const byte LoginSend = 11;
            public const byte ClientOkForLoginSend = 12;
            public const byte CreateMousePasswordSend = 13;
            public const byte ChangeMousePasswordSend = 14;
            public const byte LoginMousePasswordSend = 15;
            public const byte CreateAvatarSend2 = 17;
            public const byte DeleteAvatarSend = 18;
            public const byte ChangeAvatarNameSend = 19;
            public const byte WantGiftSend = 21;
            public const byte DemandZoneServerInfoSend = 22;
            public const byte FailMoveZone1Send = 23;
            public const byte ChangeMasterSend = 24;
            public const byte GiftInfoSend = 25;
        }

        /// <summary>LoginServer → client.</summary>
        public static class Outgoing
        {
            public const byte LoginConnectOkRecv = 0;
            public const byte LoginRecv = 11;
            public const byte UserAvatarRecv2 = 12;
            public const byte CreateMousePasswordRecv = 13;
            public const byte ChangeMousePasswordRecv = 14;
            public const byte LoginMousePasswordRecv = 15;
            public const byte CreateAvatarRecv = 17;
            public const byte DeleteAvatarRecv = 18;
            public const byte ChangeAvatarNameRecv = 19;
            public const byte WantGiftRecv = 21;
            public const byte DemandZoneServerInfoRecv = 22;

            /// <summary>
            ///     Layout resolved against LOGIN.h l.216-222 (login protocol report §5.24): 3 ints, always zero,
            ///     no XOR. Emitted at the end of every login train (success AND failure), see SEND_LOGIN.
            /// </summary>
            public const byte RecommandWorldRecv = 24;

            public const byte DemandGiftRecv = 25;

            /// <summary>Same layout and behaviour as <see cref="RecommandWorldRecv" /> (LOGIN.h shares the struct).</summary>
            public const byte RecommandWorld2Recv = 26;

            // 27 (LCP_CHANGE_MASTER_RECV) is deliberately NOT declared: dead on the legacy side too — defined in
            // LOGIN.h l.229-233 but never emitted (W_CHANGE_MASTER_SEND has an empty body, S04_MyWork02.cpp l.1655).
        }
    }

    public static class Zone
    {
        /// <summary>Client → GameServer/Zone.</summary>
        public static class Incoming
        {
            public const byte TempRegisterSend = 11;
            public const byte RegisterAvatarSend = 12;
            public const byte ClientOkForZoneSend = 13;
            public const byte AvatarActionSend = 15;
            public const byte UpdateAvatarAction = 16;
            public const byte GeneralNoticeSend = 17;
            public const byte ProcessAttackSend = 18;
            public const byte ProcessDataSend = 19;
            public const byte DemandZoneServerInfo2 = 20;
            public const byte FailMoveZone2Send = 21;
            public const byte UseHotkeyItemSend = 22;
            public const byte UseInventoryItemSend = 23;
            public const byte ImproveItemSend = 24;
            public const byte AddItemSend = 25;
            public const byte ExchangeItemSend = 26;
            public const byte HighItemSend = 27;
            public const byte LowItemSend = 28;
            public const byte MakeItemSend = 29;
            public const byte MakeSkillSend = 30;
            public const byte StartPshopSend = 31;
            public const byte EndPshopSend = 32;
            public const byte DemandPshopSend = 33;
            public const byte PshopItemInfoSend = 34;
            public const byte BuyPshopSend = 35;
            public const byte ProcessQuestSend = 36;
            public const byte GeneralChatSend = 38;
            public const byte SecretChatSend = 39;
            public const byte GeneralShoutSend = 40;
            public const byte GetCashSizeSend = 41;
            public const byte BuyCashItemSend = 42;
            public const byte DuelAskSend = 43;
            public const byte DuelCancelSend = 44;
            public const byte DuelAnswerSend = 45;
            public const byte DuelStartSend = 46;
            public const byte TradeAskSend = 47;
            public const byte TradeCancelSend = 48;
            public const byte TradeAnswerSend = 49;
            public const byte TradeStartSend = 50;
            public const byte TradeMenuSend = 51;
            public const byte TradeEndSend = 52;
            public const byte FriendAskSend = 53;
            public const byte FriendCancelSend = 54;
            public const byte FriendAnswerSend = 55;
            public const byte FriendMakeSend = 56;
            public const byte FriendFindSend = 57;
            public const byte FriendDeleteSend = 58;
            public const byte TeacherAskSend = 59;
            public const byte TeacherCancelSend = 60;
            public const byte TeacherAnswerSend = 61;
            public const byte TeacherStartSend = 62;
            public const byte TeacherEndSend = 63;
            public const byte TeacherStateSend = 64;
            public const byte PartyAskSend = 65;
            public const byte PartyCancelSend = 66;
            public const byte PartyAnswerSend = 67;
            public const byte PartyChatSend = 68;
            public const byte PartyLeaveSend = 69;
            public const byte PartyExileSend = 70;
            public const byte PartyBreakSend = 71;
            public const byte GuildAskSend = 72;
            public const byte GuildCancelSend = 73;
            public const byte GuildAnswerSend = 74;
            public const byte GuildWorkSend = 75;
            public const byte GuildNoticeSend = 76;
            public const byte GuildChatSend = 77;
            public const byte GuildFindSend = 78;
            public const byte TribeWorkSend = 79;
            public const byte TribeNoticeSend = 80;
            public const byte TribeChatSend = 81;
            public const byte TribeBankSend = 82;
            public const byte TribeVoteSend = 83;
            public const byte ChangeAutoInfo = 86;
            public const byte AnimalStateSend = 87;
            public const byte MakePetSend = 88;
            public const byte DestroyItemSend = 89;
            public const byte CostumeStateSend = 90;
            public const byte GetCashItemInfoSend = 91;
            public const byte GetZoneConnectUserSend = 92;
            public const byte SkyUpItemSend = 93;
            public const byte TimeEffectSend = 97;
            public const byte AutoConfigSend = 99;
            public const byte FishingStateSend = 103;
            public const byte FishingResultSend = 104;
            public const byte FishingRewardSend = 105;
            public const byte GetDeputyPshopSend = 108;
            public const byte SetDeputyPshopSend = 109;
            public const byte SetDeputyPshopMoneySend = 110;
            public const byte RankBuffSend = 111;
            public const byte TribeNotifySend = 112;
            public const byte AnimalAbsorbSend = 113;
            public const byte HeroRankInfoSend = 118;
            public const byte HeroRewardSend = 119;
            public const byte ChugsoungWarUpSend = 120;
            public const byte MissionCompleteSend = 126;
            public const byte UpLevelItemSend = 127;
            public const byte BottleStateSend = 129;
            public const byte MakeItem2Send = 131;
            public const byte CostumeState2Send = 139;
            public const byte DemandBloodMarkSend = 140;
            public const byte BuyBloodMarkSend = 141;
            public const byte HeartbeatSend = 151;
            public const byte WorldChatSend = 152;
            public const byte StellarStateSend = 153;
            public const byte GetRewardItemSend = 154;
            public const byte ClaimRewardItemSend = 155;
            public const byte UpdatePetActionSend = 156;
            public const byte RuneSystemSend = 157;
        }

        /// <summary>GameServer/Zone → client.</summary>
        public static class Outgoing
        {
            public const byte ConnectOkRecv = 0;
            public const byte TempRegisterRecv = 11;
            public const byte RegisterAvatarRecv = 12;
            public const byte BroadcastWorldInfo = 13;
            public const byte AvatarActionRecv = 15;
            public const byte AvatarEffectValueInfo = 16;
            public const byte AvatarChangeInfo1 = 17;
            public const byte MonsterActionRecv = 18;
            public const byte ItemActionRecv = 19;
            public const byte GeneralNoticeRecv = 20;
            public const byte ProcessAttackRecv = 21;
            public const byte AvatarChangeInfo2 = 22;
            public const byte ProcessDataRecv = 23;
            public const byte DemandZoneServerInfo2Result = 24;
            public const byte UseHotkeyItemRecv = 25;
            public const byte UseInventoryItemRecv = 26;
            public const byte ImproveItemRecv = 27;
            public const byte AddItemRecv = 28;
            public const byte ExchangeItemRecv = 29;
            public const byte HighItemRecv = 30;
            public const byte LowItemRecv = 31;
            public const byte MakeItemRecv = 32;
            public const byte MakeSkillRecv = 33;
            public const byte StartPshopRecv = 34;
            public const byte EndPshopRecv = 35;
            public const byte DemandPshopRecv = 36;
            public const byte PshopItemInfoRecv = 37;
            public const byte BuyPshopRecv = 38;
            public const byte ProcessQuestRecv = 39;
            public const byte ChangeToTribe4Recv = 40;
            public const byte GeneralChatRecv = 41;
            public const byte SecretChatRecv = 42;
            public const byte GeneralShoutRecv = 43;
            public const byte GetCashSizeRecv = 44;
            public const byte BuyCashItemRecv = 45;
            public const byte DuelAskRecv = 46;
            public const byte DuelCancelRecv = 47;
            public const byte DuelAnswerRecv = 48;
            public const byte DuelStartRecv = 49;
            public const byte DuelTimeInfo = 50;
            public const byte DuelEndRecv = 51;
            public const byte TradeAskRecv = 52;
            public const byte TradeCancelRecv = 53;
            public const byte TradeAnswerRecv = 54;
            public const byte TradeStartRecv = 55;
            public const byte TradeStateRecv = 56;
            public const byte TradeMenuRecv = 57;
            public const byte TradeEndRecv = 58;
            public const byte FriendAskRecv = 59;
            public const byte FriendCancelRecv = 60;
            public const byte FriendAnswerRecv = 61;
            public const byte FriendMakeRecv = 62;
            public const byte FriendFindRecv = 63;
            public const byte FriendDeleteRecv = 64;
            public const byte TeacherAskRecv = 65;
            public const byte TeacherCancelRecv = 66;
            public const byte TeacherAnswerRecv = 67;
            public const byte TeacherStartRecv = 68;
            public const byte TeacherEndRecv = 69;
            public const byte TeacherStateRecv = 70;
            public const byte PartyAskRecv = 71;
            public const byte PartyCancelRecv = 72;
            public const byte PartyAnswerRecv = 73;
            public const byte PartyMakeInfo = 74;
            public const byte PartyJoinInfo = 75;
            public const byte PartyChatRecv = 76;
            public const byte PartyLeaveRecv = 77;
            public const byte PartyExileRecv = 78;
            public const byte PartyBreakRecv = 79;
            public const byte GuildAskRecv = 80;
            public const byte GuildCancelRecv = 81;
            public const byte GuildAnswerRecv = 82;
            public const byte GuildWorkRecv = 83;
            public const byte GuildNoticeRecv = 84;
            public const byte GuildChatRecv = 85;
            public const byte GuildFindRecv = 86;
            public const byte GuildLoginInfo = 87;
            public const byte TribeWorkRecv = 88;
            public const byte TribeNoticeRecv = 89;
            public const byte TribeChatRecv = 90;
            public const byte TribeBankRecv = 91;
            public const byte TribeVoteRecv = 92;
            public const byte TribeAllianceInfo = 93;
            public const byte BroadcastInfoRecv = 94;
            public const byte Zone049TypeBattleInfo = 95;
            public const byte Zone051TypeBattleInfo = 96;
            public const byte GmCommandInfo = 97;
            public const byte ReturnToAutoZone = 98;
            public const byte Zone194TypeBattleInfo = 100;
            public const byte Zone194TypeBattleCountdown = 101;
            public const byte AnimalStateRecv = 102;
            public const byte Zone267TypeBattleInfo = 103;
            public const byte Zone241TypeBattleInfo = 104;
            public const byte MakePetRecv = 105;
            public const byte DestroyItemRecv = 106;
            public const byte CostumeStateRecv = 108;
            public const byte GetCashItemInfoRecv = 109;
            public const byte UpdateCashItemInfo = 110;
            public const byte GetZoneConnectUserRecv = 111;
            public const byte SkyUpItemRecv = 112;
            public const byte ContinueSkillStatRecv = 113;
            public const byte ContinueSkillUseRecv = 114;
            public const byte Zone297TypeRemainInfo = 116;
            public const byte Zone297TypeRemainMonsterInfo = 118;
            public const byte MultiItemCreateRecv = 119;
            public const byte SetHotkeyInventoryRecv = 120;
            public const byte AutoConfigRecv = 123;
            public const byte FishingStateRecv = 127;
            public const byte FishingResultRecv = 128;
            public const byte FishingRewardRecv = 129;
            public const byte DeputyPshopActionRecv = 134;
            public const byte GetDeputyPshopRecv = 135;
            public const byte SetDeputyPshopRecv = 136;
            public const byte SetDeputyPshopMoneyRecv = 137;
            public const byte AddUserInventoryItemRecv = 138;
            public const byte TribeNotifyRecv = 139;
            public const byte HeroRankInfoRecv = 148;
            public const byte HeroRewardRecv = 149;
            public const byte LastHeroRankInfoRecv = 150;
            public const byte ChugsoungWarUpRecv = 151;
            public const byte BroadcastChugsoungInfo = 152;
            public const byte MissionCompleteRecv = 163;
            public const byte UpLevelItemRecv = 164;
            public const byte DrunkInfoRecv = 165;
            public const byte MakeItem2Recv = 168;
            public const byte CostumeState2Recv = 177;
            public const byte DemandBloodMarkRecv = 178;
            public const byte BuyBloodMarkRecv = 179;
            public const byte WorldChatRecv = 192;
            public const byte StellarStateRecv = 193;
            public const byte SetInventoryItemRecv = 194;
            public const byte GetRewardItemRecv = 195;
            public const byte ClaimRewardItemRecv = 196;
            public const byte RuneSystemRecv = 199;
            public const byte FfaTypeBattleInfo = 200;
        }
    }
}
