using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.World;

public enum ZoneCommandKind : byte
{
    Enter,

    AcknowledgeEnterBootstrap,

    Leave,
    Move,
    PetAction,

    BeginZoneTransfer,

    ClearZoneTransferPending,

    RollbackZoneTransfer,

    RefreshZoneTransferRegistrationTimestamp,

    SetMuted,

    CreditRegularWarConclusion,

    GrantValleyWarRewardDrop,

    CreditZone038Occupation,

    ApplyRegularWarReward,

    SummonRegularWarBoss,

    DespawnRegularWarBosses,

    BroadcastDuelStart,

    SetRegularWarSmallestTribe,

    ApplyPvpKillRewardClaim,

    ApplyZone38TribeEffects
}

public sealed record ZoneTransferHandoffSnapshot(
    string CharacterName,
    byte Tribe,
    bool WasMovingZone,
    DateTime PreviousRegisteredAtUtc,
    DateTime PendingRegisteredAtUtc,
    int[]? PreviousBuffs);

public enum ZoneLeaveResultKind : byte
{
    Applied = 1,

    Rejected,

    Faulted,

    Unknown,

    Cancelled
}

public readonly record struct ZoneLeaveResult(
    ZoneLeaveResultKind Kind,
    PlayerRuntimeState? DepartingState,
    string? Cause = null)
{
    public static ZoneLeaveResult Applied(PlayerRuntimeState state) => new(ZoneLeaveResultKind.Applied, state);

    public static ZoneLeaveResult Rejected(string? cause = null) => new(ZoneLeaveResultKind.Rejected, null, cause);

    public static ZoneLeaveResult Faulted(PlayerRuntimeState? departingState, string? cause = null) =>
        new(ZoneLeaveResultKind.Faulted, departingState, cause);

    public static ZoneLeaveResult Unknown(string? cause = null) => new(ZoneLeaveResultKind.Unknown, null, cause);

    public static ZoneLeaveResult Cancelled(string? cause = null) =>
        new(ZoneLeaveResultKind.Cancelled, null, cause);
}

public readonly record struct ZoneLeaveSubmission(ZoneLeaveResult Result, Task<ZoneLeaveResult>? PendingResult);

public readonly struct ZoneCommand
{
    public required ZoneCommandKind Kind { get; init; }
    public required int CharacterId { get; init; }

    public ActionInfo Action { get; init; }

    public bool IsResumeAction { get; init; }

    public PlayerEnterData? EnterData { get; init; }

    public bool Muted { get; init; }

    public DateTime ZoneTransferRegisteredAtUtc { get; init; }

    public short ZoneTransferTargetMapId { get; init; }

    public byte WinningTribe { get; init; }

    public byte SmallestPresentTribe { get; init; }

    public RegularWarRewardGrant RegularWarReward { get; init; }

    public PvpKillCooldownClaim PvpKillCooldownClaim { get; init; }

    public Zone38TribeEffectSnapshot Zone38TribeEffects { get; init; }

    public int DuelOpponentCharacterId { get; init; }

    public int DuelUniqueNumber { get; init; }

    public long ExpectedSessionId { get; init; }

    public TaskCompletionSource<ZoneLeaveResult>? LeaveCompletion { get; init; }

    public TaskCompletionSource<PlayerRuntimeState?>? EnterSnapshot { get; init; }

    public TaskCompletionSource<ZoneCommandResult>? Completion { get; init; }

    public TaskCompletionSource<ZoneTransferHandoffSnapshot?>? ZoneTransferSnapshot { get; init; }

    public static ZoneCommand Enter(int characterId, PlayerEnterData data,
        TaskCompletionSource<PlayerRuntimeState?>? snapshotSignal = null)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.Enter, CharacterId = characterId, EnterData = data, EnterSnapshot = snapshotSignal
        };
    }

    public static ZoneCommand AcknowledgeEnterBootstrap(int characterId, long expectedSessionId,
        TaskCompletionSource<ZoneCommandResult> completion)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.AcknowledgeEnterBootstrap,
            CharacterId = characterId,
            ExpectedSessionId = expectedSessionId,
            Completion = completion
        };
    }

    public static ZoneCommand Leave(int characterId, long expectedSessionId,
        TaskCompletionSource<ZoneLeaveResult> completion)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.Leave,
            CharacterId = characterId,
            ExpectedSessionId = expectedSessionId,
            LeaveCompletion = completion
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

    public static ZoneCommand BeginZoneTransfer(int characterId, short targetMapId,
        TaskCompletionSource<ZoneTransferHandoffSnapshot?> snapshot,
        TaskCompletionSource<ZoneCommandResult> completion)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.BeginZoneTransfer,
            CharacterId = characterId,
            ZoneTransferTargetMapId = targetMapId,
            ZoneTransferSnapshot = snapshot,
            Completion = completion
        };
    }

    public static ZoneCommand ClearZoneTransferPending(int characterId, DateTime zoneTransferRegisteredAtUtc = default)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.ClearZoneTransferPending, CharacterId = characterId,
            ZoneTransferRegisteredAtUtc = zoneTransferRegisteredAtUtc
        };
    }

    public static ZoneCommand ClearZoneTransferPending(int characterId,
        TaskCompletionSource<ZoneCommandResult> completion)
    {
        return ClearZoneTransferPending(characterId, default, completion);
    }

    public static ZoneCommand ClearZoneTransferPending(int characterId, DateTime zoneTransferRegisteredAtUtc,
        TaskCompletionSource<ZoneCommandResult> completion)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.ClearZoneTransferPending, CharacterId = characterId,
            ZoneTransferRegisteredAtUtc = zoneTransferRegisteredAtUtc, Completion = completion
        };
    }

    public static ZoneCommand RollbackZoneTransfer(int characterId, DateTime pendingRegisteredAtUtc,
        TaskCompletionSource<ZoneCommandResult> completion)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.RollbackZoneTransfer,
            CharacterId = characterId,
            ZoneTransferRegisteredAtUtc = pendingRegisteredAtUtc,
            Completion = completion
        };
    }

    public static ZoneCommand RefreshZoneTransferRegistrationTimestamp(int characterId)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.RefreshZoneTransferRegistrationTimestamp, CharacterId = characterId
        };
    }

    public static ZoneCommand RefreshZoneTransferRegistrationTimestamp(int characterId,
        TaskCompletionSource<ZoneCommandResult> completion)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.RefreshZoneTransferRegistrationTimestamp, CharacterId = characterId,
            Completion = completion
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

    public static ZoneCommand ApplyPvpKillRewardClaim(in PvpKillCooldownClaim claim)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.ApplyPvpKillRewardClaim,
            CharacterId = claim.AttackerCharacterId,
            PvpKillCooldownClaim = claim
        };
    }

    public static ZoneCommand ApplyZone38TribeEffects(Zone38TribeEffectSnapshot snapshot)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.ApplyZone38TribeEffects,
            CharacterId = 0,
            Zone38TribeEffects = snapshot
        };
    }

    public static ZoneCommand SummonRegularWarBoss()
    {
        return new ZoneCommand { Kind = ZoneCommandKind.SummonRegularWarBoss, CharacterId = 0 };
    }

    public static ZoneCommand DespawnRegularWarBosses(TaskCompletionSource<ZoneCommandResult> completion)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.DespawnRegularWarBosses, CharacterId = 0, Completion = completion
        };
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
    short AccountGrade,
    int UserSort,
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
    int ProtectForDeath = 0,
    int AnimalDoubleExp = 0,
    int DmgBoost = 0,
    int HPBoost = 0,
    int CriBoost = 0,
    ImmutableArray<(int ItemId, int ExpActivity, int Power)>? MountGarageSlots = null,
    int SkillPoints = 0,
    int ProtectForHalo = 0,
    int BonusItemLevel = 0,
    bool BonusItemValue = false,
    int TribeNotifyScrollCount = 0,
    int TribeFourReturnAllowance = 0,
    int ProtectForRefine = 0,
    int ProtectForDestroy = 0,
    int ProtectForCostume = 0,
    int ProtectForDestroy2 = 0,
    int LodRounds = 0,
    int StellarCoreIndex = -1,
    ImmutableArray<int>? StellarCoreWardrobe = null,
    ImmutableArray<int>? StellarCoreExpireDate = null,
    int EliteDungeonTime = 0,
    int DungeonKeyTime = 0,
    int IvyHallTicketTime = 0,
    int ScrollOfSeekersTime = 0,
    int FightingGodForDestroy = 0,
    long Money = 0);
