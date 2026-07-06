namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Single source of truth for the legacy simulation clock: the legacy zone runs its logic off a 500 ms
///     tick (TimeLogic=500), i.e. 2 Hz -- separate from Fenrir's 20 Hz network frame. All legacy durations
///     (buffs, stun, IA, regen, respawn) are counted in these ticks.
/// </summary>
/// <remarks>
///     Anti-x10 warning: a legacy tick is 500 ms, not 50 ms, and is 10x the network frame period. Never
///     convert a legacy tick count by multiplying with the network frame period -- always go through
///     <see cref="ToTimeSpan" /> / <see cref="ToWholeLegacyTicks" />.
/// </remarks>
public static class SimulationClock
{
    /// <summary>One legacy simulation tick: 500 ms (TimeLogic=500, ServerInfo.ini line 143).</summary>
    public const int LegacyTickMilliseconds = 500;

    /// <summary>Monster respawn-scan cadence: every 20 legacy ticks (~10 s). Consumed by MonsterSpawnScheduler.</summary>
    public const int MonsterRespawnScanLegacyTicks = 20;

    /// <summary>Pet activity decay cadence: -1 every 60 legacy ticks (30 s). Consumed by PetActivitySystem.</summary>
    public const int PetActivityDecayLegacyTicks = 60;

    /// <summary>
    ///     Stun/knockdown duration countdown cadence: -1 every 2 legacy ticks (~1 s, strict equality gate in
    ///     the legacy -- <c>S07_MyGame04.cpp:378-380,438-459</c>). Consumed by StunCountdownSystem.
    /// </summary>
    public const int StunCountdownLegacyTicks = 2;

    /// <summary>
    ///     Post-death territorial revive-eligibility recheck (side effect 1, <c>S07_MyGame04.cpp:257-327</c>):
    ///     begins once this many legacy ticks (5 s) have elapsed since death, then re-runs every subsequent
    ///     tick (not a one-shot) until it succeeds or <see cref="AntiAbuseForceQuitLegacyTicks" /> fires first.
    ///     Consumed by <see cref="DeathGateTickSystem" />.
    /// </summary>
    public const int ReviveEligibilityLegacyTicks = 10;

    /// <summary>
    ///     Death-gate broadcast-suppression threshold (side effect 3,
    ///     <c>
    ///         S07_MyGame03.cpp:809-833,857-880,
    ///         893-935,954-978
    ///     </c>
    ///     ): a still-flagged recipient stops receiving proximity-broadcast avatar traffic
    ///     once this many legacy ticks (15 s) have elapsed since their own death. Always strictly between
    ///     <see cref="ReviveEligibilityLegacyTicks" /> and <see cref="AntiAbuseForceQuitLegacyTicks" />.
    /// </summary>
    public const int DeathBroadcastSuppressionLegacyTicks = 30;

    /// <summary>
    ///     <c>mProtect_ReviveHack</c> anti-abuse force-quit safety valve (side effect 2,
    ///     <c>
    ///         S07_MyGame04.cpp:
    ///         328-332
    ///     </c>
    ///     ): a session still flagged this many legacy ticks (25 s) after death is torn down
    ///     outright. Consumed by <see cref="DeathGateTickSystem" />.
    /// </summary>
    public const int AntiAbuseForceQuitLegacyTicks = 50;

    public static readonly TimeSpan LegacyTick = TimeSpan.FromMilliseconds(LegacyTickMilliseconds);

    /// <summary>Keep-alive re-broadcast cadence for avatar positions: 3.5 s (tLogicAvatarTick).</summary>
    public static readonly TimeSpan AvatarRebroadcastInterval = TimeSpan.FromSeconds(3.5);

    /// <summary>Keep-alive re-broadcast cadence for monster state: 5 s (tLogicMonsterTick).</summary>
    public static readonly TimeSpan MonsterRebroadcastInterval = TimeSpan.FromSeconds(5);

    /// <summary>Keep-alive re-broadcast cadence for ground items: 5 s (tLogicItemTick).</summary>
    public static readonly TimeSpan GroundItemRebroadcastInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Per-shop periodic proxy/deputy-shop re-broadcast throttle: 5 s, independent per shop -- not a cadence
    ///     shared across the whole table. Server/ts25zone/S07_MyGame01.cpp:2600-2607.
    /// </summary>
    public static readonly TimeSpan ProxyShopRebroadcastInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     TimeSpan convenience form of <see cref="ReviveEligibilityLegacyTicks" />, for test call sites driving
    ///     <c>Zone.Tick</c> directly.
    /// </summary>
    public static readonly TimeSpan ReviveEligibilityDelay = ToTimeSpan(ReviveEligibilityLegacyTicks);

    /// <summary>Ground item lifetime: 60 000 ms from creation.</summary>
    public static readonly TimeSpan GroundItemLifetime = TimeSpan.FromMilliseconds(60_000);

    /// <summary>A dropped item becomes free for any player to pick up this long after creation.</summary>
    public static readonly TimeSpan GroundItemFreeForAllDelay = TimeSpan.FromSeconds(30);

    /// <summary>When the killer was in a party at drop time, the party can pick it up starting this long after creation.</summary>
    public static readonly TimeSpan GroundItemPartyShareDelay = TimeSpan.FromSeconds(10);

    public static TimeSpan ToTimeSpan(int legacyTicks)
    {
        return legacyTicks * LegacyTick;
    }

    /// <summary>Fractional remainder is discarded (callers that must not lose it use SimulationTickAccumulator).</summary>
    public static int ToWholeLegacyTicks(TimeSpan duration)
    {
        return duration <= TimeSpan.Zero ? 0 : (int)(duration.Ticks / LegacyTick.Ticks);
    }
}
