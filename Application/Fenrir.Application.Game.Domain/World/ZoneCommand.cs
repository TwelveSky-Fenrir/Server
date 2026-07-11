using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Stats;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public enum ZoneCommandKind : byte
{
    Enter,
    Leave,
    Move,
    PetAction,

        MarkZoneTransferPending,

        SetMuted,

        CreditRegularWarConclusion,

        GrantValleyWarRewardDrop,

        CreditZone038Occupation,

        ApplyRegularWarReward,

        SummonRegularWarBoss
}

public readonly struct ZoneCommand
{
    public required ZoneCommandKind Kind { get; init; }
    public required int CharacterId { get; init; }

        public ActionInfo Action { get; init; }

        public bool IsResumeAction { get; init; }

        public PlayerEnterData? EnterData { get; init; }

        public Zone? HandoffTarget { get; init; }

        public (float X, float Y, float Z)? HandoffPosition { get; init; }

        public bool Muted { get; init; }

        public byte WinningTribe { get; init; }

        public RegularWarRewardGrant RegularWarReward { get; init; }

    public static ZoneCommand Enter(int characterId, PlayerEnterData data)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.Enter, CharacterId = characterId, EnterData = data };
    }

    public static ZoneCommand Leave(int characterId, Zone? handoffTarget = null,
        (float X, float Y, float Z)? handoffPosition = null)
    {
        return new ZoneCommand
        {
            Kind = ZoneCommandKind.Leave, CharacterId = characterId, HandoffTarget = handoffTarget,
            HandoffPosition = handoffPosition
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
    int InventoryDate = 0,
    int StoreDate = 0,
    int PetBagDate = 0,
    string? SourceIp = null,
    ImmutableArray<int>? RuneSystem = null,
    ImmutableArray<int>? RuneSystemStat = null,
    int M15PetLuckyBoxPity = 0);
