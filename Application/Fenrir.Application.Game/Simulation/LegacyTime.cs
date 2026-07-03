namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     The single source of truth for the legacy simulation clock (plan decision D4). The legacy zone runs
///     <c>MyGame::Logic</c> off <c>[Zone.Server] TimeLogic=500</c> ms (ServerDocs/30_Fenrir_ServerLogic/05 §0) —
///     every legacy duration (buffs, stun, IA monstres, regen, respawn) is COUNTED IN TICKS OF 500 MS, i.e. the
///     legacy simulation is 2 Hz. Fenrir keeps its 20 Hz network frame, but simulation time only ever advances in
///     whole legacy ticks delivered by <see cref="LegacyTickAccumulator" />, converted through THIS constant and
///     nothing else.
/// </summary>
/// <remarks>
///     Anti-×10 warning (completeness review, report 08): a legacy tick is 500 ms = HALF A SECOND, not 50 ms —
///     and it is 10× the 50 ms network frame of <c>GameServerOptions.TickRateHz</c> = 20 Hz. Never convert a
///     legacy tick count by multiplying with the network frame period: "20 ticks" in legacy code means ~10 s
///     (<c>mTickCount % 20</c>, report 05 §0), not 1 s. All conversions must go through
///     <see cref="ToTimeSpan" /> / <see cref="ToWholeLegacyTicks" />.
/// </remarks>
public static class LegacyTime
{
    /// <summary>One legacy simulation tick: 500 ms (<c>TimeLogic=500</c>, ServerInfo.ini line 143 — 2 Hz).</summary>
    public const int LegacyTickMilliseconds = 500;

    /// <summary><see cref="LegacyTickMilliseconds" /> as a <see cref="TimeSpan" />.</summary>
    public static readonly TimeSpan LegacyTick = TimeSpan.FromMilliseconds(LegacyTickMilliseconds);

    /// <summary>
    ///     Keep-alive re-broadcast cadence for avatar positions: 3.5 s (<c>tLogicAvatarTick = 3.5f</c>, report 05
    ///     §0 item 6 — re-emitted via <c>B_AVATAR_ACTION_RECV2</c> in the legacy loop, <c>ZcAvatarActionRecv</c>
    ///     to AOI neighbors here).
    /// </summary>
    public static readonly TimeSpan AvatarRebroadcastInterval = TimeSpan.FromSeconds(3.5);

    /// <summary>
    ///     Keep-alive re-broadcast cadence for monster state: 5 s (<c>tLogicMonsterTick = 5.0f</c>, report 05 §0
    ///     item 7). TODO(Phase C — V4 monstres): consumed by the monster entity pool's rebroadcast step once
    ///     <c>MonsterEntity</c> exists; declared now so the verified legacy cadence lives next to its siblings.
    /// </summary>
    public static readonly TimeSpan MonsterRebroadcastInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Keep-alive re-broadcast cadence for ground items: 5 s (<c>tLogicItemTick</c>, report 05 §0 item 8).
    ///     TODO(Phase C — V4 loot): consumed by the ground-item pool's rebroadcast step once
    ///     <c>GroundItemEntity</c> exists.
    /// </summary>
    public static readonly TimeSpan GroundItemRebroadcastInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Death → automatic revive delay: 10 legacy ticks = 5 s (report 12 §4.2: "après 10 ticks (~5 s),
    ///     mCheckDeath est levé automatiquement"). Consumed by <see cref="World.Zone.ApplyDeath" />.
    /// </summary>
    public static readonly TimeSpan DeathReviveDelay = ToTimeSpan(10);

    /// <summary>
    ///     Real duration of <paramref name="legacyTicks" /> legacy ticks (e.g. a 20-legacy-tick buff = 10 s,
    ///     never 1 s — see the anti-×10 remark on this class).
    /// </summary>
    public static TimeSpan ToTimeSpan(int legacyTicks)
    {
        return legacyTicks * LegacyTick;
    }

    /// <summary>
    ///     How many WHOLE legacy ticks fit in <paramref name="duration" /> — the fractional remainder is
    ///     discarded (callers that must not lose it accumulate through <see cref="LegacyTickAccumulator" />).
    /// </summary>
    public static int ToWholeLegacyTicks(TimeSpan duration)
    {
        return duration <= TimeSpan.Zero ? 0 : (int)(duration.Ticks / LegacyTick.Ticks);
    }
}
