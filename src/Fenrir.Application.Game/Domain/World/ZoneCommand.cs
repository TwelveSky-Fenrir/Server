using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.World;

public enum ZoneCommandKind : byte
{
    Enter,
    Leave,
    Move,
    PetAction,

    MarkZoneTransferPending,

    ClearZoneTransferPending,

    RefreshZoneTransferRegistrationTimestamp,

    SetMuted,

    CreditRegularWarConclusion,

    GrantValleyWarRewardDrop,

    CreditZone038Occupation,

    ApplyRegularWarReward,

    SummonRegularWarBoss,

    BroadcastDuelStart,

    SetRegularWarSmallestTribe
}

public readonly struct ZoneCommand
{
    public required ZoneCommandKind Kind { get; init; }
    public required int CharacterId { get; init; }

    public ActionInfo Action { get; init; }

    public bool IsResumeAction { get; init; }

    public PlayerEnterData? EnterData { get; init; }

    public bool Muted { get; init; }

    public byte WinningTribe { get; init; }

    public byte SmallestPresentTribe { get; init; }

    public RegularWarRewardGrant RegularWarReward { get; init; }

    public int DuelOpponentCharacterId { get; init; }

    public int DuelUniqueNumber { get; init; }

        public TaskCompletionSource<PlayerRuntimeState?>? LeaveSnapshot { get; init; }

    public static ZoneCommand Enter(int characterId, PlayerEnterData data)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.Enter, CharacterId = characterId, EnterData = data };
    }

    public static ZoneCommand Leave(int characterId, TaskCompletionSource<PlayerRuntimeState?>? snapshotSignal = null)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.Leave, CharacterId = characterId, LeaveSnapshot = snapshotSignal
        };
    }

    public static ZoneCommand Move(int characterId, in ActionInfo action, bool isResumeAction = false)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.Move, CharacterId = characterId, Action = action,
            IsResumeAction = isResumeAction
        };
    }

    public static ZoneCommand PetAction(int characterId, in ActionInfo action)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.PetAction, CharacterId = characterId, Action = action };
    }

    public static ZoneCommand MarkZoneTransferPending(int characterId)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.MarkZoneTransferPending, CharacterId = characterId };
    }

    public static ZoneCommand ClearZoneTransferPending(int characterId)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.ClearZoneTransferPending, CharacterId = characterId };
    }

    public static ZoneCommand RefreshZoneTransferRegistrationTimestamp(int characterId)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.RefreshZoneTransferRegistrationTimestamp, CharacterId = characterId
        };
    }

    public static ZoneCommand SetMuted(int characterId, bool muted)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.SetMuted, CharacterId = characterId, Muted = muted };
    }

    public static ZoneCommand CreditRegularWarConclusion(int characterId)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.CreditRegularWarConclusion, CharacterId = characterId };
    }

    public static ZoneCommand GrantValleyWarRewardDrop(int characterId)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.GrantValleyWarRewardDrop, CharacterId = characterId };
    }

    public static ZoneCommand CreditZone038Occupation(int characterId, byte winningTribe)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.CreditZone038Occupation, CharacterId = characterId, WinningTribe = winningTribe
        };
    }

    public static ZoneCommand ApplyRegularWarReward(RegularWarRewardGrant grant)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.ApplyRegularWarReward, CharacterId = grant.CharacterId, RegularWarReward = grant
        };
    }

    public static ZoneCommand SummonRegularWarBoss()
    {
        return new ZoneCommand { Kind = ZoneCommandKind.SummonRegularWarBoss, CharacterId = 0 };
    }

    public static ZoneCommand SetRegularWarSmallestTribe(byte tribeId)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.SetRegularWarSmallestTribe, CharacterId = 0, SmallestPresentTribe = tribeId
        };
    }

    public static ZoneCommand BroadcastDuelStart(int requesterCharacterId, int opponentCharacterId,
        int duelUniqueNumber)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.BroadcastDuelStart, CharacterId = requesterCharacterId,
            DuelOpponentCharacterId = opponentCharacterId, DuelUniqueNumber = duelUniqueNumber
        };
    }
}

public sealed record PlayerEnterData(
    IPacketSession Session,
    string Name,
    byte Tribe,
    byte Gender,
    byte HeadType,
    byte FaceType,
    short Level,
    short MapId,
    float PosX,
    float PosY,
    float PosZ,
    float Heading,
    int Life,
    int MaxLife,
    int Mana,
    int MaxMana,
    long FlushSequence,
    bool IsDead = false,
    IReadOnlyList<CharacterItemSlotDto>? Items = null,
    EffectiveStats? Stats = null,
    bool IsMuted = false,
    int? GuildId = null,
    string GuildName = "",
    byte GuildRoleDb = 0,
    byte TribeRole = 0,
    IReadOnlyDictionary<byte, int>? FriendsBySlot = null,
    IReadOnlyList<CharacterSkillDto>? Skills = null,
    int? TeacherCharacterId = null,
    int? StudentCharacterId = null,
    QuestProgress QuestProgress = default,
    int MissionJoinWar = 0,
    int MissionKillOtherTribe = 0,
    int MissionKillMonster = 0,
    int MissionPlayTime = 0,
    bool AutoHuntEnabled = false,
    AutoHunt? AutoHuntConfig = null,
    byte AutoLifeRatio = 0,
    byte AutoManaRatio = 0,
    int PetGrowth = 0,
    byte PetActivity = 0,
    string GuildCallName = "",
    int GuildBuffType = 0,
    bool GuildBuffActive = false,
    int StatVit = 0,
    int StatStr = 0,
    int StatInt = 0,
    int StatDex = 0,
    int StatPoints = 0,
    int Title = 0,
    int Halo = 0,
    int RebirthCount = 0,
    long Experience = 0,
    int ContributionPoints = 0,
    int TeacherPoint = 0,
    short Level2 = 0,
    int Exp2 = 0,
    int KnownCashCatalogVersion = PlayerRuntimeState.CashCatalogVersionUnknown,
    int TicksSinceDeath = 0,
    bool ReviveHackFlag = false,
    bool CanUseConsumables = true,
    int DeathSubCounter = 0,
    int DungeonInstanceRoundsRemaining = 0,
    int HeroRankPoints = 0,
    int EatLifePotion = 0,
    int EatManaPotion = 0,
    int EatStrPotion = 0,
    int EatDexPotion = 0,
    int EatElePotion = 0,
    int DropItemTime = 0,
    int WarPoint = 0,
    long PremiumExpireUtc = 0,
    int BuffX2Time = 0,
    byte PreviousTribe = 0,
    BuffInfo? Buffs = null,
    IReadOnlyList<CharacterHotkeyDto>? Hotkeys = null,
    int PetExpX2Time = 0,
    int Zone241Time = 0,
    long StoreMoney = 0,
    int BigMoney = 0,
    int InventoryDate = 0,
    int StoreDate = 0,
    int PetBagDate = 0,
    string? SourceIp = null,
    ImmutableArray<int>? RuneSystem = null,
    ImmutableArray<int>? RuneSystemStat = null,
    int M15PetLuckyBoxPity = 0,
    int ActionSort = 0,
    int ActionSkillNumber = 0,
    int ActionSkillGradeNum1 = 0,
    int ActionSkillGradeNum2 = 0,
    int PetActionSort = 0,
    float PetActionFront = 0,
    float PetActionTargetLocationX = 0,
    float PetActionTargetLocationY = 0,
    float PetActionTargetLocationZ = 0,
    ImmutableArray<(int ItemId, int Count)>? BottleSlots = null,
    int? DrunkBottleIndex = null,
    int? DrunkBottleTicksRemaining = null,
    int MountItemId = 0,
    int MountExpActivity = 0,
    int MountPower = 0,
    int MountSlotIndex = -1,
    int MountTime = 0,
    int VisibleState = 1,
    int SpecialState = 0,
    bool UseOrnament = false,
    int BloodCoin = 0,
    int AnimalAbsorbTime = 0,
    int AnimalAbsorbState = 0,
    int CostumeIndex = -1,
    ImmutableArray<int>? CostumeWardrobe = null,
    ImmutableArray<int>? CostumeDate = null,
    ImmutableArray<int>? CostumeExpireDate = null,
    int AutoBuffTime = 0,
    ImmutableArray<(int SkillId, int Grade)>? AutoBuffSkill = null,
    int RankPointDate = 0,
    int RankBuffType = 0,
    int AutoHuntPaidDayBudget = 0,
    int AutoHuntPaidMinuteBudget = 0,
    int RankPoint = 0,
    int CloakLuckyBoxPity = 0,
    int CloakVariantBoxPity = 0,
    int MountVariantBoxPity = 0,
    int ImproveItemValue = 0,
    int AddItemValue = 0,
    int HighItemValue = 0,
    int TaiyanKeyTimer = 0,
    int PlayTime1 = 0,
    int PlayTime3 = 0,
    bool HsbStoneRewardClaimed = false,
    int TowerCpMilestoneCounter = 0,
    int WarriorPill = 0,
    int WarriorScroll = 0,
    int SilverTime = 0,
    int GoldTime = 0,

        int DoubleKillNumTime = 0,

        int DoubleKillExpTime = 0,

        int DoubleKillNumTime2 = 0,

        int ProtectForDeath = 0);
