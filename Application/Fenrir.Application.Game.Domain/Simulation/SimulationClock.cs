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
///     <para>
///         Legacy trivia: the shipped <c>TimeLogic=500</c> is actually armed/consumed as 499 ms at
///         runtime, due to a bootstrap-time -1 adjustment the legacy applies once and never revisits. See
///         <see cref="LegacyTickMilliseconds" />'s own remarks for the full citation and why Fenrir
///         intentionally keeps a flat 500 rather than reproducing it.
///     </para>
/// </remarks>
public static class SimulationClock
{
    /// <summary>One legacy simulation tick: 500 ms (TimeLogic=500, ServerInfo.ini line 143).</summary>
    /// <remarks>
    ///     The legacy zone never actually runs on this literal ini value. <c>SetTimeLogic</c>
    ///     (<c>Server/Header/socket.h:3-14</c>) decrements the loaded <c>TimeLogic</c> by 1 (floored at 1)
    ///     and writes the decremented value back in place (<c>socket.h:8</c>) before both arming the
    ///     Win32 <c>SetTimer</c> callback and computing the <c>dTime</c> fed into <c>mGAME.Logic()</c>
    ///     (<c>Server/ts25zone/S02_MyServer.cpp:348-354,412-420</c>). So the shipped
    ///     <c>TimeLogic=500</c> (<c>Server/BuildEU33/ServerInfo.ini:143</c>) is actually armed and consumed
    ///     as 499 ms, not 500 ms, for the rest of that process's life.
    ///     <para>
    ///         Fenrir deliberately keeps this constant at a flat 500, not 499. The cited material never
    ///         establishes a gameplay rationale for the -1 -- it reads as an artifact of arming a Win32
    ///         <c>SetTimer</c> callback from the legacy's single-threaded main-window loop, not a
    ///         deliberate tuning choice -- and <c>Zone.RunAsync</c> has no equivalent step to reproduce it
    ///         against: Fenrir never arms a fixed-period OS timer for the logic tick at all, it measures
    ///         real elapsed wall-clock time via <c>Stopwatch</c> on a <c>PeriodicTimer</c> and feeds that
    ///         measured value into <see cref="SimulationTickAccumulator" />. The resulting ~0.2% cadence
    ///         difference (499 ms vs 500 ms) is functionally negligible for every gameplay timer derived
    ///         from this constant -- switching it to 499 would also silently rescale every other constant
    ///         in this file whose doc comment states its real-world duration assuming a flat 500 ms (e.g.
    ///         <see cref="PlayTimeAccrualLegacyTicks" />'s "full real minute"), for no verified behavioral
    ///         benefit. This is a deliberate, recorded decision, not an oversight -- revisit only if a
    ///         specific system is found to need byte-exact parity with the legacy's 499 ms constant.
    ///     </para>
    /// </remarks>
    public const int LegacyTickMilliseconds = 500;

    /// <summary>Monster respawn-scan cadence: every 20 legacy ticks (~10 s). Consumed by MonsterSpawnScheduler.</summary>
    public const int MonsterRespawnScanLegacyTicks = 20;

    /// <summary>
    ///     Monster proactive-aggro detection-check throttle (<c>mCheckDetectEnemyTime</c>,
    ///     <c>S07_MyGame05.cpp:127-131</c>): a detection scan may run at most once every 2 legacy ticks (~1 s),
    ///     restarting on every attempted check -- successful or not, not just on an actual acquisition. Consumed
    ///     by MonsterAiSystem.
    /// </summary>
    public const int MonsterDetectionThrottleLegacyTicks = 2;

    /// <summary>Pet activity decay cadence: -1 every 60 legacy ticks (30 s). Consumed by PetActivitySystem.</summary>
    public const int PetActivityDecayLegacyTicks = 60;

    /// <summary>
    ///     Post-chase-loss idle in-place re-detection grace period (<c>mCheckFirstLocationTime</c>,
    ///     <c>S07_MyGame05.cpp:1012</c>): a monster idling with no re-acquired target only starts heading home
    ///     after this many continuous legacy ticks (60 s at TimeLogic=500ms) with zero engagement -- not the
    ///     instant it gives up its chase target. Consumed by MonsterAiSystem.
    /// </summary>
    public const int MonsterIdleReturnHomeLegacyTicks = 120;

    /// <summary>
    ///     Idle random-short-wander fallback cadence (<c>mCheckLastWalkTime</c>, <c>S07_MyGame05.cpp:1033</c>),
    ///     only reached on a given idle tick when <see cref="MonsterIdleReturnHomeLegacyTicks" /> has not itself
    ///     just fired (40 s at TimeLogic=500ms). Consumed by MonsterAiSystem.
    /// </summary>
    public const int MonsterIdleWanderLegacyTicks = 80;

    /// <summary>
    ///     Play-time accrual cadence (<c>S07_MyGame04.cpp:887-911</c>'s strict
    ///     <c>(mGAME.mTickCount - user-&gt;mTickCountFor01Minute) == 120</c> gate): a full real minute is 120
    ///     legacy ticks at TimeLogic=500ms (<c>ServerInfo.ini:143</c>). Consumed by PlayTimeAccrualSystem.
    /// </summary>
    public const int PlayTimeAccrualLegacyTicks = 120;

    /// <summary>
    ///     Stun/knockdown duration countdown cadence: -1 every 2 legacy ticks (~1 s, strict equality gate in
    ///     the legacy -- <c>S07_MyGame04.cpp:378-380,438-459</c>). Consumed by StunCountdownSystem.
    /// </summary>
    public const int StunCountdownLegacyTicks = 2;

    /// <summary>
    ///     Passive sit/meditate HP+MP regen cadence: fires once every 2 legacy ticks (~1 s), the exact same
    ///     shared <c>mTickCountFor01Second == 2</c> strict-equality gate as <see cref="StunCountdownLegacyTicks" />
    ///     (<c>S07_MyGame04.cpp:378-380</c>, regen block at <c>:461-519</c>, shared scope's closing brace at
    ///     <c>:825</c>). Consumed by MeditationRegenSystem -- without this gate the full ~1-second regen amount
    ///     would apply every 500 ms legacy tick instead, twice the legacy rate.
    /// </summary>
    public const int MeditationRegenLegacyTicks = 2;

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

    /// <summary>
    ///     Deterministic per-entity phase offset for a periodic keep-alive cadence (<see cref="AvatarRebroadcastInterval" />,
    ///     <see cref="MonsterRebroadcastInterval" />, <see cref="GroundItemRebroadcastInterval" />), so that many
    ///     entities seeded from the exact same <c>Zone</c> clock value within one tick -- every configured monster
    ///     spawn-region slot popping unconditionally on a zone's first <c>MonsterSpawnScheduler.Simulate</c> call, a
    ///     multi-item kill drop spawning several ground items in one <c>ProcessDeath</c> call, or a burst of queued
    ///     <c>Enter</c> commands (e.g. a post-restart reconnect storm) draining within a single <c>Zone.DrainInbox</c>
    ///     call -- don't all become "due" for their first keep-alive rebroadcast on the exact same subsequent tick
    ///     (a thundering herd: every one of them re-broadcasts in the same frame, then re-synchronizes and repeats
    ///     every <paramref name="interval" /> thereafter, since the tick that services them all also re-stamps them
    ///     all to that same later clock value).
    ///     <para>
    ///         Deterministic and stable per <paramref name="entityId" /> -- built from the entity's own natural
    ///         monotonic id (<c>MonsterEntity.ServerIndex</c>, a ground item's own spawn index, a player's
    ///         <c>CharacterId</c>), never <see cref="Random" />/<see cref="DateTime.Now" />, so tests stay
    ///         reproducible and two runs with the same spawn order stagger identically.
    ///     </para>
    ///     <para>
    ///         Bucketed in whole <see cref="LegacyTick" /> units (10 buckets for a 5 s interval, 7 for 3.5 s) rather
    ///         than at finer (e.g. millisecond) granularity: <c>Zone.RebroadcastMonsters</c>/<c>RebroadcastAvatars</c>/
    ///         <c>RebroadcastGroundItems</c> only ever evaluate "is this entity due yet" once per <c>Zone.Tick</c>
    ///         call that has at least one whole legacy tick elapsed (<c>legacyTicksElapsed &gt; 0</c>), so two
    ///         entities whose offsets fall inside the same legacy tick are indistinguishable in practice -- a finer
    ///         bucket count would waste precision without spreading anything across an ADDITIONAL real evaluation
    ///         pass. Sizing buckets to whole legacy ticks instead spreads consecutive entity ids (the common case:
    ///         a zone's spawn-region slots, or ground items from one kill, are assigned small sequential ids) across
    ///         the FULL window -- entity <c>id</c> and <c>id + bucketCount</c> land in the same bucket, but id and
    ///         <c>id + 1</c> land in DIFFERENT, evenly-spread buckets -- rather than clustering every low id into
    ///         the window's opening moment the way a much-larger bucket count would for any population smaller than
    ///         that count.
    ///     </para>
    ///     <para>
    ///         Callers subtract the returned offset from the clock value they would otherwise stamp verbatim (e.g.
    ///         <c>monster.LastRebroadcastAt = _clock - RebroadcastStaggerOffset(...)</c>), which only ever pulls an
    ///         entity's first due time EARLIER within the current interval, never later -- so the existing
    ///         "rebroadcast at least once every <paramref name="interval" />" contract is preserved verbatim; only
    ///         the exact tick within that window shifts.
    ///     </para>
    /// </summary>
    public static TimeSpan RebroadcastStaggerOffset(int entityId, TimeSpan interval)
    {
        var bucketCount = (int)(interval.Ticks / LegacyTick.Ticks);
        if (bucketCount <= 0)
            return TimeSpan.Zero;

        var bucket = ((entityId % bucketCount) + bucketCount) % bucketCount;
        return TimeSpan.FromTicks(interval.Ticks * bucket / bucketCount);
    }

    /// <summary>Fractional remainder is discarded (callers that must not lose it use SimulationTickAccumulator).</summary>
    public static int ToWholeLegacyTicks(TimeSpan duration)
    {
        return duration <= TimeSpan.Zero ? 0 : (int)(duration.Ticks / LegacyTick.Ticks);
    }
}
