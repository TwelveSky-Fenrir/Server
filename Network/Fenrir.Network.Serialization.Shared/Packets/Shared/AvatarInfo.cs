using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(11168)]
public readonly partial record struct AvatarInfo : IFenrirWireType<AvatarInfo>
{
    public required int VisibleState { get; init; }
    public required int SpecialState { get; init; }
    public required int PlayTime1 { get; init; }
    public required int PlayTime2 { get; init; }
    public required int KillOtherTribe { get; init; }

    [FixedString(13)] public required string Name { get; init; }

    // 3-byte pad after Name (char[13]) absorbed by Tribe.
    [Reserved(3)] public required int Tribe { get; init; }
    public required int PreviousTribe { get; init; }
    public required int Gender { get; init; }
    public required int HeadType { get; init; }
    public required int FaceType { get; init; }
    public required int Level1 { get; init; }
    public required int Level2 { get; init; }
    public required int Exp1 { get; init; }
    public required int Exp2 { get; init; }
    public required int Vit { get; init; }
    public required int Str { get; init; }
    public required int Int { get; init; }
    public required int Dex { get; init; }
    public required int EatLifePotion { get; init; }
    public required int EatManaPotion { get; init; }
    public required int StatPoint { get; init; }
    public required int SkillPoint { get; init; }

    [FixedArray(52)] public required int[] Equip { get; init; }
    public required int InventoryDate { get; init; }
    public required int Money { get; init; }

    [FixedArray(768)] public required int[] Inventory { get; init; }
    public required int TradeMoney { get; init; }

    [FixedArray(32)] public required int[] Trade { get; init; }
    public required int StoreDate { get; init; }
    public required int StoreMoney { get; init; }

    [FixedArray(224)] public required int[] StoreItem { get; init; }
    [FixedArray(80)] public required int[] Skill { get; init; }
    [FixedArray(126)] public required int[] HotKey { get; init; }
    [FixedArray(5)] public required int[] QuestInfo { get; init; }

    [FixedArray(10)] [FixedString(13)] public required string[] Friend { get; init; }

    [FixedString(13)] public required string Teacher { get; init; }

    // Teacher/Student are consecutive char[13]; already 4-byte aligned, no padding.
    [FixedString(13)] public required string Student { get; init; }
    public required int TeacherPoint { get; init; }

    [FixedString(13)] public required string GuildName { get; init; }

    // 3-byte pad after GuildName absorbed by GuildRole.
    [Reserved(3)] public required int GuildRole { get; init; }

    [FixedString(5)] public required string CallName { get; init; }

    // 3-byte pad after CallName absorbed by GuildMarkNum.
    [Reserved(3)] public required int GuildMarkNum { get; init; }
    public required int GuildMarkEffect { get; init; }

    [FixedArray(6)] public required int[] LogoutInfo { get; init; }
    public required int ProtectForDeath { get; init; }
    public required int ProtectForDestroy { get; init; }
    public required int FightingGodForDestroy { get; init; }
    public required int DoubleExpTime1 { get; init; }
    public required int DoubleKillNumTime { get; init; }
    public required int DoubleKillExpTime { get; init; }
    public required int Zone175Time { get; init; }
    public required int Zone101Time { get; init; }
    public required int Zone125Time { get; init; }
    public required int Zone126Time { get; init; }
    public required int KillMonsterNum { get; init; }
    public required int KillMonsterNum2 { get; init; }
    public required int KillMonsterNum3 { get; init; }
    public required int LevelZoneKeyNum { get; init; }
    public required int SearchAndBuyDate { get; init; }
    public required int LifePotionConvertNum { get; init; }
    public required int ManaPotionConvertNum { get; init; }
    public required int TribeVoteDate { get; init; }
    public required int AutoLifeRatio { get; init; }
    public required int AutoManaRatio { get; init; }
    public required int EatStrPotion { get; init; }
    public required int EatDexPotion { get; init; }

    [FixedArray(10)] public required int[] Animal { get; init; }
    public required int AnimalIndex { get; init; }
    public required int AnimalTime { get; init; }
    public required int AddItemValue { get; init; }
    public required int HighItemValue { get; init; }
    public required int DropItemTime { get; init; }
    public required int Title { get; init; }
    public required int DoubleKillNumTime2 { get; init; }
    public required int Halo { get; init; }
    public required int BonusItemValue { get; init; }
    public required int BonusItemLevel { get; init; }
    public required int KillOtherTribeEvent { get; init; }
    public required int TeacherPointEvent { get; init; }
    public required int PlayTimeEvent { get; init; }
    public required int ProtectForHalo { get; init; }
    public required int UseOrnament { get; init; }
    public required int SilverTime { get; init; }
    public required int Zone234Time { get; init; }
    public required int Zone235Time { get; init; }
    public required int Zone236Time { get; init; }
    public required int Zone237Time { get; init; }
    public required int Zone238Time { get; init; }
    public required int Zone239Time { get; init; }
    public required int Zone240Time { get; init; }
    public required int RebirthNum { get; init; }
    public required int Zone241Time { get; init; }
    public required int ChallengeNum { get; init; }
    public required int GoldTime { get; init; }
    public required int Zone234X2Time { get; init; }
    public required int Zone235X2Time { get; init; }
    public required int BattleEnterNum { get; init; }
    public required int BattleEnterDate { get; init; }
    public required int PlayTime3 { get; init; }
    public required int BuffX2Time { get; init; }
    public required int DoubleExpTime2 { get; init; }
    public required int ReturnTribeNum { get; init; }
    public required int PetExpX2Time { get; init; }
    public required int GuildMoneyTime { get; init; }
    public required int SaveMoney { get; init; }

    [FixedArray(112)] public required int[] SaveItem { get; init; }

    [FixedArray(5)] [FixedString(13)] public required string[] PartyName { get; init; }

    // 3-byte pad after PartyName absorbed by Costume.
    [FixedArray(10)] [Reserved(3)] public required int[] Costume { get; init; }
    [FixedArray(10)] public required int[] CostumeDate { get; init; }
    [FixedArray(10)] public required int[] CostumeExpireDate { get; init; }
    public required int CostumeIndex { get; init; }
    public required int DmgBoost { get; init; }
    public required int HPBoost { get; init; }
    public required int CriBoost { get; init; }
    public required int AutoBuffTime { get; init; }

    [FixedArray(16)] public required int[] AutoBuffSkill { get; init; }
    public required int DungeonEvent { get; init; }
    public required int ImproveItemValue { get; init; }
    public required int Zone270Score { get; init; }
    public required int TimeEffectTime { get; init; }
    public required int StateTimeEffect { get; init; }
    public required int SelectTimeEffectType { get; init; }

    [FixedArray(384)] public required int[] InvenSocket { get; init; }
    [FixedArray(39)] public required int[] EquipSocket { get; init; }
    [FixedArray(24)] public required int[] TradeSocket { get; init; }
    [FixedArray(168)] public required int[] StoreSocket { get; init; }
    [FixedArray(84)] public required int[] SaveSocket { get; init; }
    public required int AutoTime { get; init; }
    public required int AutoTime2 { get; init; }
    public required int AutoState { get; init; }
    public required AutoHunt AutoHunt { get; init; }
    public required int BigMoney { get; init; }
    public required int BigTradeMoney { get; init; }
    public required int BigStoreMoney { get; init; }
    public required int BigSaveMoney { get; init; }
    public required int EatElePotion { get; init; }
    public required int Zone050Time { get; init; }
    public required int ProtectForRefine { get; init; }
    public required int GoldenLakeTime { get; init; }
    public required int PreventForSmelt { get; init; }
    public required int BloodCoin { get; init; }
    public required int HeavenlyTicket { get; init; }
    public required int Tevushi { get; init; }
    public required int MammothRoad { get; init; }
    public required int Zone050Time2 { get; init; }
    public required int FishingZoneDate { get; init; }
    public required int ProxyShopDate { get; init; }
    public required int B4GHPElixir { get; init; }
    public required int B4GMPElixir { get; init; }
    public required int B4GStrElixir { get; init; }
    public required int B4GDexElixir { get; init; }
    public required int KillCount { get; init; }
    public required int KillCountTime { get; init; }
    public required int RankPoint { get; init; }
    public required int RankPointDate { get; init; }
    public required int RankBuffType { get; init; }
    public required int TribeNotifyNum { get; init; }
    public required int SpeakerCount { get; init; }

    [FixedArray(10)] public required int[] AnimalExpActivity { get; init; }
    [FixedArray(10)] public required int[] AnimalPower { get; init; }
    public required int AnimalDoubleExp { get; init; }
    public required int EventValue1 { get; init; }
    public required int EventValue2 { get; init; }
    public required int EventValue3 { get; init; }
    public required int EventValue4 { get; init; }
    public required int AnimalAbsorbTime { get; init; }
    public required int AnimalAbsorbState { get; init; }
    public required int CapsuleCashPoint { get; init; }
    public required int RageGauge { get; init; }
    public required int RageBuffTime { get; init; }
    public required int RageBuffState { get; init; }
    public required int RageBuffPotion { get; init; }
    public required int RageDoubleEffect { get; init; }
    public required int WarriorScroll { get; init; }
    public required int UseItemMonth { get; init; }
    public required int UseItemOnce { get; init; }
    public required int HeroPoint { get; init; }
    public required int HeroPointDate { get; init; }
    public required int KillMonsterNumForHeroPoint { get; init; }
    public required int WarriorPill { get; init; }
    public required int OllehEventPoint { get; init; }
    public required int ProtectForWing { get; init; }
    public required int TeacherPointDate { get; init; }
    public required int ProtectForDestroy2 { get; init; }
    public required int PetBagDate { get; init; }

    [FixedArray(20)] public required int[] PetBag { get; init; }
    public required MissionDate MissionDate { get; init; }
    public required int CriticalSpeedTime { get; init; }
    public required int DefenceUpNum { get; init; }
    public required int Zone038Ticket { get; init; }
    public required int HyunKyungMonster { get; init; }

    [FixedArray(10)] public required int[] Bottle { get; init; }
    [FixedArray(10)] public required int[] BottleCount { get; init; }
    public required int BottleIndex { get; init; }
    public required int BottleTime { get; init; }

    [FixedArray(28)] public required int[] MixSkill { get; init; }
    public required int MixSkillBuffTime { get; init; }
    public required int BywonjiTime { get; init; }
    public required int UniqueSkill { get; init; }
    public required int UniqueSkillBuffTime { get; init; }
    public required int BackSoul { get; init; }

    // Premium (8 bytes) already 8-aligned after BackSoul; no padding needed.
    public required long Premium { get; init; }
    public required int ProtectForCombine { get; init; }
    public required int PlayOnlineTime { get; init; }
    public required int PlayOnlineTime2 { get; init; }
    public required int ProtectForCostume { get; init; }
    public required int WarPoint { get; init; }
    public required int PopUpKillAvt { get; init; }
    public required int PopUpKillMonster { get; init; }
    public required int PopUpKillAvtWar { get; init; }

    [FixedArray(10)] public required int[] StellarCore { get; init; }
    [FixedArray(10)] public required int[] StellarCoreExpireDate { get; init; }
    public required int StellarCoreIndex { get; init; }

    [FixedArray(128)] public required int[] InvenExpireDate { get; init; }
    [FixedArray(13)] public required int[] EquipExpireDate { get; init; }
    [FixedArray(8)] public required int[] TradeExpireDate { get; init; }
    [FixedArray(56)] public required int[] StoreExpireDate { get; init; }
    [FixedArray(28)] public required int[] SaveExpireDate { get; init; }
    public required int WarKillCount { get; init; }
    public required int HSBStoneRewardCheck { get; init; }
    public required int GBox2249 { get; init; }
    public required int GBox8114 { get; init; }
    public required int GBox8115 { get; init; }
    public required int GBox8111 { get; init; }

    [FixedArray(4)] public required int[] RuneSystem { get; init; }
    [FixedArray(4)] public required int[] RuneSystemStat { get; init; }
}
