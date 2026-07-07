using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Stats;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public enum ZoneCommandKind : byte
{
    Enter,
    Leave,
    Move,
    PetAction,

    /// <summary>
    ///     Sets <see cref="PlayerRuntimeState.IsMovingZone" /> true on this zone's own tracked copy of
    ///     <see cref="ZoneCommand.CharacterId" /> -- the ONLY legal way to do so, preserving the single-writer
    ///     invariant every other <see cref="PlayerRuntimeState" /> mutation already relies on. Posted by
    ///     <c>Fenrir.Application.Game.Services.ZoneLifecycle.ZoneMoveService.HandleCrossShardAsync</c>'s
    ///     successful-handoff branch, from the request thread that resolved the destination shard -- never
    ///     mutated directly from there. See <see cref="PlayerRuntimeState.IsMovingZone" />'s own remarks.
    /// </summary>
    MarkZoneTransferPending,

    /// <summary>
    ///     Sets <see cref="PlayerRuntimeState.IsMuted" /> to <see cref="ZoneCommand.Muted" /> on this zone's own
    ///     tracked copy of <see cref="ZoneCommand.CharacterId" /> -- the ONLY legal way to update it once a
    ///     character is already tracked here (world entry seeds the initial value directly, before the player
    ///     is added to this zone's own map, so no race is possible there), same single-writer posture as
    ///     <see cref="MarkZoneTransferPending" />. Posted by <c>MuteRefreshPollHost</c>
    ///     (<c>Application/Fenrir.Application.Game.Hosting</c>) from its own background-timer thread, once per
    ///     poll interval, for every online character whose batch-refreshed mute status changed.
    /// </summary>
    SetMuted
}

/// <summary>
///     <c>Zone</c>'s internal ABI: a hand-discriminated union, not a class hierarchy -- no allocation per
///     command, only <see cref="Kind" /> decides which field is meaningful.
/// </summary>
public readonly struct ZoneCommand
{
    public required ZoneCommandKind Kind { get; init; }
    public required int CharacterId { get; init; }

    /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.Move" />.</summary>
    public ActionInfo Action { get; init; }

    /// <summary>
    ///     Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.Move" />: true for
    ///     CZ_UPDATE_AVATAR_ACTION (op16), false (default) for CZ_AVATAR_ACTION_SEND (op15). The two opcodes
    ///     run their own, separate Sort/Type legality switch in the legacy source -- see
    ///     <see cref="Fenrir.Application.Game.Domain.Movement.AvatarActionResumeWhitelist" /> and
    ///     <see cref="Fenrir.Application.Game.Domain.Movement.CharacterMotionWhitelist" />'s own remarks --
    ///     so <c>Zone.HandleMove</c> needs this to pick the correct one.
    /// </summary>
    public bool IsResumeAction { get; init; }

    /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.Enter" />.</summary>
    public PlayerEnterData? EnterData { get; init; }

    /// <summary>
    ///     Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.Leave" />: null for a plain
    ///     leave, or the target zone for an in-process handoff -- the source tick snapshots state and posts
    ///     the matching Enter there itself.
    /// </summary>
    public Zone? HandoffTarget { get; init; }

    /// <summary>
    ///     Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.Leave" /> with a
    ///     <see cref="HandoffTarget" />: overrides the position snapshotted into the target's Enter. Null keeps
    ///     the player's current position. Lets a resolved portal/respawn arrival point reach the target zone
    ///     without the poster touching <see cref="PlayerRuntimeState" /> directly.
    /// </summary>
    public (float X, float Y, float Z)? HandoffPosition { get; init; }

    /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.SetMuted" />.</summary>
    public bool Muted { get; init; }

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

    /// <summary>op156 CZ_UPDATE_PET_ACTION_SEND -- reuses <see cref="Action" />, only its pet sub-fields are meaningful.</summary>
    public static ZoneCommand PetAction(int characterId, in ActionInfo action)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.PetAction, CharacterId = characterId, Action = action };
    }

    /// <summary>See <see cref="ZoneCommandKind.MarkZoneTransferPending" />'s own remarks.</summary>
    public static ZoneCommand MarkZoneTransferPending(int characterId)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.MarkZoneTransferPending, CharacterId = characterId };
    }

    /// <summary>See <see cref="ZoneCommandKind.SetMuted" />'s own remarks.</summary>
    public static ZoneCommand SetMuted(int characterId, bool muted)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.SetMuted, CharacterId = characterId, Muted = muted };
    }
}

/// <summary>
///     Snapshot handed to <see cref="Zone" /> when a player finishes registration and is ready to be simulated
///     -- everything the tick needs to seed a <see cref="PlayerRuntimeState" /> without touching SQL itself.
///     <see cref="Items" />/<see cref="Stats" /> are the caller's already-computed rows; <c>Zone.HandleEnter</c>
///     only ever copies them, never computes them.
/// </summary>
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
    // These, and every field down through PetActivity below, must travel here too, or PlayerRuntimeState's
    // quest/mission/auto-hunt/pet-growth fields silently reset to their zero/empty/disabled default on every
    // in-process zone transfer -- same "must travel or it resets" posture as the StatVit/Title/HeroRankPoints
    // block further below. See ZoneTransfer.CreateEnterData, which carries the live source-zone values through
    // instead of leaving these at their defaults.
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
    // Cosmetic in-guild title (game.GuildMembers.CallName); "" when none set or guildless.
    string GuildCallName = "",
    // These raw progression fields must travel here or PlayerRuntimeState.StatVit/StatStr/StatInt/StatDex/
    // Title/Halo/RebirthCount/Experience/ContributionPoints/StatPoints silently reset to 0 on every world
    // entry and zone transfer.
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
    // Quest reward type 5 (wAvatar.aTeacherPoint) -- see PlayerRuntimeState.TeacherPoint's own remarks.
    int TeacherPoint = 0,
    // Same "must travel here or it silently resets" posture as the block above -- see
    // PlayerRuntimeState.Level2/Exp2's own remarks.
    short Level2 = 0,
    int Exp2 = 0,
    // Defaults to PlayerRuntimeState.CashCatalogVersionUnknown -- correct for a brand-new login. An
    // in-process zone transfer must pass the source PlayerRuntimeState's own value instead, or a client mid-
    // notify window would silently lose its "please re-ask" state on every map hop -- see
    // ZoneTransfer.CreateEnterData and PlayerRuntimeState.KnownCashCatalogVersion's own remarks.
    int KnownCashCatalogVersion = PlayerRuntimeState.CashCatalogVersionUnknown,
    // Death-gate state (PlayerRuntimeState.TicksSinceDeath/ReviveHackFlag/DeathSubCounter) -- must travel here
    // too, or a player mid-death who transfers zones would silently lose their territorial-eligibility/
    // anti-abuse tick counters. CanUseConsumables defaults true (matches a fresh login/non-dead arrival).
    int TicksSinceDeath = 0,
    bool ReviveHackFlag = false,
    bool CanUseConsumables = true,
    int DeathSubCounter = 0,
    // Zone-241 "LOD" personal-dungeon quota -- see PlayerRuntimeState.DungeonInstanceRoundsRemaining's own
    // remarks (no persisted source populates a non-zero value yet).
    int DungeonInstanceRoundsRemaining = 0,
    // The character's persisted Current-period hero-rank point total (EnterWorldService's world-entry
    // hydration read, legacy MyDB::GetHeroPoint) -- must travel here too, or PlayerRuntimeState.HeroRankPoints
    // silently resets to 0 on every world entry AND every in-process zone transfer (see that field's own
    // remarks and ZoneTransfer.CreateEnterData, which carries the live in-memory value through a same-shard
    // hop instead of re-querying it).
    int HeroRankPoints = 0,
    // Stat/elixir-potion lifetime counters (PlayerRuntimeState.EatLifePotion/EatManaPotion/EatStrPotion/
    // EatDexPotion/EatElePotion) -- must travel here too, or they silently reset to 0 on every world entry
    // and in-process zone transfer, exactly the same "must travel or it resets" posture as the StatVit/
    // Title/HeroRankPoints block above. game.Characters already persists all five (0-400 CHECK-constrained);
    // this was the missing runtime-hydration link (item-usage-consumables finding, Critical).
    int EatLifePotion = 0,
    int EatManaPotion = 0,
    int EatStrPotion = 0,
    int EatDexPotion = 0,
    int EatElePotion = 0,
    // mSupportSkillTimeUpRatio's two source fields (behavior contract "buff-application-stacking-decay") --
    // must travel here too, same "must travel or it resets" posture as every block above: without this, a
    // premium account's buff-duration multiplier would silently reset to 1x on every in-process zone
    // transfer. PremiumExpireUtc defaults 0 (no premium) for a brand-new login too, but EnterWorldService's
    // own call site overrides it with the character's real persisted game.Characters.PremiumExpireUtc --
    // see PlayerRuntimeState.PremiumExpireUtc's own remarks. BuffX2Time has no persisted source anywhere yet
    // (PlayerRuntimeState.BuffX2Time's own remarks) so it stays 0 on every path until a future item-
    // consumption pass adds one.
    long PremiumExpireUtc = 0,
    int BuffX2Time = 0,
    // The transferring character's full live BUFF_INFO (Server/ts25zone/S04_MyWork02.cpp:2017-2186's
    // DEMAND_ZONE_SERVER_INFO_2 -> REGISTER_AVATAR_SEND broker round trip, Server/Header/Protocol/STRUCT.h:1462).
    // Null on a fresh login: EnterWorldService does not (yet) hydrate PlayerRuntimeState.Buffs from persisted
    // rows -- only the client-facing entry snapshot does, via EnterWorldService.BuildBuffInfo -- so a brand-new
    // session simply starts with PlayerRuntimeState.Buffs at its zero default, same as before this field
    // existed. A same-shard zone transfer always supplies a non-null value here (ZoneTransfer.CreateEnterData,
    // via ZoneTransferBuffRules.Resolve), which also encodes the one legacy exception: the destination zone
    // being zone 124 (Pungun Hall) wipes the table instead of carrying it through.
    BuffInfo? Buffs = null,
    // World-entry hydration (EnterWorldService, from game.CharacterHotkeys) or, for an in-process handoff,
    // the live table carried through by ZoneTransfer.CreateEnterData -- see PlayerRuntimeState.Hotkeys' own
    // remarks. Null means "seed nothing" (a brand-new character has no rows yet), never "clear the table".
    IReadOnlyList<CharacterHotkeyDto>? Hotkeys = null,
    // Pet EXP boost pill running duration counter -- must travel here too, or PlayerRuntimeState.PetExpX2Time
    // silently resets to 0 on every in-process zone transfer, same "must travel or it resets" posture as
    // PetGrowth/PetActivity above. No persisted source exists (PlayerRuntimeState.PetExpX2Time's own remarks),
    // so it stays 0 on a fresh world entry, same posture as BuffX2Time.
    int PetExpX2Time = 0);
