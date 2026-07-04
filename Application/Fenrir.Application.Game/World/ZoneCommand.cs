using Fenrir.Application.Game.Stats;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.World;

public enum ZoneCommandKind : byte
{
    Enter,
    Leave,
    Move
}

/// <summary>
///     <c>Zone</c>'s internal ABI (architecture reference §10.2): a hand-discriminated union, not a class
///     hierarchy — no allocation per command, only <see cref="Kind" /> decides which of the remaining fields is
///     meaningful. <see cref="Enter" />/<see cref="Leave" />/<see cref="Move" /> are the only producers; nothing
///     outside this file should construct one directly.
/// </summary>
public readonly struct ZoneCommand
{
    public required ZoneCommandKind Kind { get; init; }
    public required int CharacterId { get; init; }

    /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.Move" />.</summary>
    public ActionInfo Action { get; init; }

    /// <summary>Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.Enter" />.</summary>
    public PlayerEnterData? EnterData { get; init; }

    /// <summary>
    ///     Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.Leave" />: null for a plain
    ///     leave (disconnect), or the zone the player is transferring to (in-process handoff, ADR-0012) — the
    ///     source tick snapshots the live state and posts the matching Enter there itself.
    /// </summary>
    public Zone? HandoffTarget { get; init; }

    /// <summary>
    ///     Meaningful only when <see cref="Kind" /> is <see cref="ZoneCommandKind.Leave" /> AND
    ///     <see cref="HandoffTarget" /> is not null: overrides the position <see cref="ZoneTransfer.CreateEnterData" />
    ///     snapshots into the target's <c>Enter</c>, instead of the player's current in-zone position. Null means
    ///     "keep the current position" (a plain zone-boundary handoff, unaffected). Non-null is how a resolved
    ///     portal/NPC-transfer arrival point (<c>ZoneMoveHandler</c>) or a resolved
    ///     death-respawn spot (<see cref="Zone.ApplyDeath" />) reaches the target zone's <c>Enter</c> WITHOUT the
    ///     poster ever touching <see cref="PlayerRuntimeState" /> directly (single-writer invariant, architecture
    ///     reference §10.1) — the override travels as plain data inside this command and is applied by the
    ///     SOURCE zone's own tick, exactly like every other field here.
    /// </summary>
    public (float X, float Y, float Z)? HandoffPosition { get; init; }

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

    public static ZoneCommand Move(int characterId, in ActionInfo action)
    {
        return new ZoneCommand { Kind = ZoneCommandKind.Move, CharacterId = characterId, Action = action };
    }
}

/// <summary>
///     Snapshot handed to <see cref="Zone" /> when a player finishes registration (CZ_REGISTER_AVATAR_SEND,
///     wire contract §5.3) and is ready to be simulated. Everything the tick needs to seed a
///     <see cref="PlayerRuntimeState" /> without querying <c>Fenrir.Data</c> itself — the zone tick never touches
///     SQL directly (architecture reference §10.5: "SQL est un journal de durabilité, pas un participant au
///     gameplay"). This is also why <see cref="Items" />/<see cref="Stats" /> below are the caller's
///     ALREADY-COMPUTED equipment rows and effective stats (<c>EnterWorldHandler</c> resolves these
///     against <c>WorldDataCache</c> before ever posting this command) rather than raw catalog lookups Zone
///     itself would have to perform — <c>Zone.HandleEnter</c> only ever copies them onto the new
///     <see cref="PlayerRuntimeState" />, never computes them.
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
    TimeSpan? ReviveRemaining = null,
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
    Quests.QuestProgress QuestProgress = default,
    int MissionJoinWar = 0,
    int MissionKillOtherTribe = 0,
    int MissionKillMonster = 0,
    int MissionPlayTime = 0,
    bool AutoHuntEnabled = false,
    Contracts.Packets.Shared.AutoHunt? AutoHuntConfig = null,
    byte AutoLifeRatio = 0,
    byte AutoManaRatio = 0,
    int PetGrowth = 0,
    byte PetActivity = 0,
    // Phase C/V7 Guilds & Tribes: cosmetic in-guild title (game.GuildMembers.CallName) -- "" when none
    // set or when guildless, same posture as GuildName/GuildRoleDb.
    string GuildCallName = "",
    // CORRECTION (review finding, Phase C/V7): these raw progression fields are read from the DB into
    // EnterWorldHandler's own `character` DTO and used ONCE to compute the world-entry EffectiveStats
    // snapshot (see Stats above), but were never themselves carried into PlayerRuntimeState -- so
    // PlayerRuntimeState.StatVit/StatStr/StatInt/StatDex/Title/Halo/RebirthCount/Experience/
    // ContributionPoints/StatPoints silently reset to 0/default on EVERY world entry AND zone transfer,
    // regardless of the real persisted value. Harmless for the FIRST stat computation (EnterWorldHandler
    // computes EffectiveStats directly from the DB row, bypassing PlayerRuntimeState entirely), but any
    // LATER recompute that reads these raw fields off PlayerRuntimeState directly (equip/unequip, enchant,
    // halo purchase, stat-point reset, any V7 feature gated on Title/Halo/ContributionPoints) would then
    // wrongly operate on zero instead of the persisted value.
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
    int TeacherPoint = 0);
