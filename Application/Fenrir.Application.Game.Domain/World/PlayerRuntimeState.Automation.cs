using System.Collections.Immutable;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    private static readonly ImmutableArray<(int SkillId, int Grade)> DefaultAutoBuffSkill =
    [
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0)
    ];

    /// <summary>Legacy <c>aAutoState</c> (0/1) -- CZ_AUTO_CONFIG_SEND/ZC_AUTO_CONFIG_RECV (opcode 99/123).</summary>
    public bool AutoHuntEnabled { get; set; }

    /// <summary>
    ///     The raw 112-byte AUTO_HUNT blob, copied verbatim from the client with no server-side content
    ///     validation -- matches the legacy exactly (an anti-cheat surface deliberately left open). Null =
    ///     never configured. The autonomous bot loop itself is out of scope for this pass; only the
    ///     config-storage/gating half is implemented.
    /// </summary>
    public AutoHunt? AutoHuntConfig { get; set; }

    /// <summary>Legacy <c>aAutoLifeRatio</c> (0-5) -- CZ_CHANGE_AUTO_INFO, silently stored, never echoed back.</summary>
    public byte AutoLifeRatio { get; set; }

    /// <summary>Legacy <c>aAutoManaRatio</c> (0-5) -- same posture as <see cref="AutoLifeRatio" />.</summary>
    public byte AutoManaRatio { get; set; }

    /// <summary>
    ///     Zone-clock instant of this character's last CZ_HERORANK_INFO_SEND reply for the previous period (ZC 148) --
    ///     2.5s per-user throttle. Null = never queried yet.
    /// </summary>
    public TimeSpan? LastHeroRankingPreviousQueryAtZoneClock { get; set; }

    /// <summary>
    ///     Same throttle posture as <see cref="LastHeroRankingPreviousQueryAtZoneClock" />, for the current period (ZC
    ///     150).
    /// </summary>
    public TimeSpan? LastHeroRankingCurrentQueryAtZoneClock { get; set; }

    /// <summary>
    ///     aAutoBuffSkill[8] -- CZ_CONTINUE_SKILL_STAT_SEND (op94) registered auto-buff (skillId, grade) slots.
    ///     Session-scoped only, same "no persisted column yet" posture as <see cref="MountGarage" />.
    /// </summary>
    public ImmutableArray<(int SkillId, int Grade)> AutoBuffSkill { get; set; } = DefaultAutoBuffSkill;

    /// <summary>
    ///     aAutoBuffTime (YYYYMMDD, <see cref="Simulation.GameDate" /> encoding) -- gates CZ_CONTINUE_SKILL_USE_SEND
    ///     Sort=1. Same "real but currently unreachable" posture as <see cref="AnimalAbsorbTime" />: no
    ///     acquisition path (the unimplemented UseInventoryItem cash-boost family) exists yet, so this stays 0.
    /// </summary>
    public int AutoBuffTime { get; set; }

    /// <summary>
    ///     aStateTimeEffect -- CZ_TIME_EFFECT_SEND (op97) reward tier currently applied (120/180/240/300/360), 0 =
    ///     none. SetTimeEffect's downstream drop/exp-rate multipliers (mItemDropUpRatio etc.) are not modeled --
    ///     see <see cref="Handlers.PlaytimeBuffHandler" />'s remarks.
    /// </summary>
    public int StateTimeEffect { get; set; }

    /// <summary>
    ///     aRankBuffType -- CZ_RANK_BUFF_SEND (op111) active buff tier (1-7), 0 = none. MyFactor.cpp's per-tier
    ///     stat bonuses are not modeled yet -- see <see cref="Handlers.RankBuffHandler" />'s remarks.
    /// </summary>
    public int RankBuffType { get; set; }
}
