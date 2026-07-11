using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

/// <summary>
///     Character-specific extension of <see cref="IWriteBehindFlusher" /> -- deliberately NOT added to that
///     shared interface itself, since <c>WriteBehindFlusher&lt;TKey&gt;</c> (the generic drain-loop class)
///     implements <see cref="IWriteBehindFlusher" /> directly and is reused for several unrelated key/value
///     domains (tower-war state, hero-rank points, world state, monster loot, proxy-shop expiry, death
///     event-log, tribe-bank tax, monster boss respawn) that have no notion of "one character's live
///     Position/Vitals/Progression state." <see cref="PositionWriteBehindHost" /> is this interface's one
///     production implementation.
/// </summary>
public interface ICharacterWriteBehindFlusher : IWriteBehindFlusher
{
    /// <summary>
    ///     Synchronously (awaited) persists exactly one character's CURRENT Position/Vitals/Progression state,
    ///     read directly from the zone's live player registry at the moment this is called, instead of waiting
    ///     for the shared periodic/threshold/immediate-signal drain to get around to it. Exists for the
    ///     disconnect path: the caller MUST invoke this BEFORE posting the zone's own Leave command for the
    ///     same character, so this read can never race the zone tick's own registry removal the way a
    ///     signal-only <see cref="IWriteBehindFlusher.RequestImmediateFlush" /> can -- see
    ///     <see cref="PositionWriteBehindHost.FlushCharacterNowAsync" />'s own remarks for the full citation and
    ///     the FlushSequence-collision handling. A no-op if the character is no longer found in any zone's live
    ///     registry by the time this runs.
    /// </summary>
    public ValueTask FlushCharacterNowAsync(int characterId, CancellationToken ct);
}

/// <summary>
///     Flushes dirty-tracked character positions to <see cref="CharacterRepository.PersistPositionsAsync" />,
///     AND (see <see cref="ProgressWriteBehindHost" />'s remarks for why this is the one and only consumer of
///     the shared <see cref="DirtyTracker{TKey}" />) the Vitals/Progression side of the SAME drained batch via
///     <see cref="ProgressWriteBehindHost.FlushAsync" />. Reads CURRENT state from
///     <see cref="ZoneRegistry.TryGetPlayer" /> since the dirty tracker only holds flags, never values. Exposed
///     as <see cref="IWriteBehindFlusher" /> so a disconnecting session can request an immediate, targeted
///     flush of BOTH position and progress in one call, and as <see cref="ICharacterWriteBehindFlusher" /> for
///     the synchronous, targeted single-character disconnect flush (<see cref="FlushCharacterNowAsync" />).
/// </summary>
/// <remarks>
///     <para>
///         <b>Process-kill residual risk, exact bound:</b> this is the highest-value and highest-frequency
///         write-behind cache in the process (every combat tick and every movement re-dirties a character), so
///         its <see cref="WriteBehindFlusher{TKey}" /> deliberately runs at the DEFAULT cadence
///         (<see cref="WriteBehindFlusher{TKey}.DefaultInterval" /> = 5s,
///         <see cref="WriteBehindFlusher{TKey}.DefaultEntityThreshold" />
///         = 512 dirty characters) rather than a shorter one: unlike the low-frequency-dirty caches
///         (<c>TowerWarState</c>, <c>HeroRankPointAccumulator</c>, <c>MonsterBossRespawnTracker</c>), a
///         character actively moving or fighting is realistically dirty on almost every drain cycle, so
///         tightening this interval scales DB round-trips with online-CCU × movement/combat rate, not with a
///         rare event -- exactly the "meaningful DB load" tradeoff not worth taking here. The bound on data
///         lost to a true (non-graceful) process kill is therefore: for a character who disconnected
///         gracefully first, effectively zero (<see cref="FlushCharacterNowAsync" /> already persisted their
///         final Position+Progress synchronously before their Leave command posted); for a character still
///         connected at the instant of the kill, up to 5 seconds of Position/Vitals/Progression mutation (HP,
///         XP, level, stat/skill points, CP), or less if <see cref="WriteBehindFlusher{TKey}.DefaultEntityThreshold" />
///         was crossed first server-wide. This is an accepted, bounded tradeoff, not a silent gap.
///     </para>
/// </remarks>
public sealed class PositionWriteBehindHost : BackgroundService, ICharacterWriteBehindFlusher
{
    private readonly ICharacterRepository _characters;
    private readonly WriteBehindFlusher<int> _flusher;

    // Serializes the periodic drain callback against FlushCharacterNowAsync: both independently read a live
    // PlayerRuntimeState.FlushSequence and persist at a value derived from that read, with no other coordination
    // between them. Without this lock, the periodic drain picking up the SAME character concurrently with its
    // own disconnect flush can race two writes against usp_Character_PersistBatch/PersistProgressBatch's
    // "strictly greater than stored" idempotence guard, silently no-op'ing whichever one loses -- reproducing
    // the exact ts25playuser "silently drops the more recent save" failure mode (ServerDocs/13_ts25playuser/
    // 02_DB_Worker_Thread_Persistence.md §4, mSaveData2.insert() not replacing an existing key) this class was
    // built to avoid. Coarse-grained (one lock for every character, not per-character) is deliberate: both
    // sides are already bulk/occasional operations, never a per-tick hot path, so serializing them fully is
    // cheap and removes the race entirely rather than only narrowing it.
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly ILogger<PositionWriteBehindHost> _logger;
    private readonly ZoneRegistry _zones;

    public PositionWriteBehindHost(ZoneRegistry zones, DirtyTracker<int> dirtyTracker, ICharacterRepository characters,
        ProgressWriteBehindHost progress, ILogger<PositionWriteBehindHost> logger)
    {
        _zones = zones;
        _characters = characters;
        _logger = logger;

        _flusher = new WriteBehindFlusher<int>(
            dirtyTracker,
            async (dirty, ct) =>
            {
                await _flushGate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    // Progress always runs first and "wins" the shared per-character FlushSequence slot for this
                    // cycle -- see ProgressWriteBehindHost's remarks. claimedByProgress tells us which characters'
                    // Position row must be deferred (re-marked dirty for the next drain) instead of being persisted
                    // with the SAME FlushSequence value usp_Character_PersistBatch's guard has now already seen.
                    var claimedByProgress = await progress.FlushAsync(dirty, ct).ConfigureAwait(false);

                    var rows = new List<CharacterPositionTvp>(dirty.Count);

                    foreach (var (characterId, flags) in dirty)
                    {
                        if ((flags & DirtyFlags.Position) == 0)
                            continue;

                        if (claimedByProgress.Contains(characterId))
                        {
                            // Deferred, not dropped: Position is already documented best-effort/eventually-consistent
                            // (self-heals on the player's next move, or is caught by the disconnect path's own
                            // synchronous FlushCharacterNowAsync) -- Progression is the exploit/durability-critical
                            // side and must never lose its turn.
                            dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);
                            continue;
                        }

                        if (zones.TryGetPlayer(characterId, out var state))
                            rows.Add(new CharacterPositionTvp(characterId, state.FlushSequence, state.MapId, state.PosX,
                                state.PosY, state.PosZ, state.Heading));
                    }

                    // A player absent from every zone (logged out, or mid-handoff) is correctly dropped here -- for
                    // a true disconnect their last position was already flushed synchronously by
                    // FlushCharacterNowAsync BEFORE the zone ever removed them from its live registry, and a
                    // handoff re-marks them dirty on arrival.
                    await characters.PersistPositionsAsync(rows, ct).ConfigureAwait(false);
                }
                finally
                {
                    _flushGate.Release();
                }
            },
            onFlushError: ex => logger.LogError(ex, "Character write-behind flush failed (position and/or progress)"));
    }

    public void RequestImmediateFlush()
    {
        _flusher.RequestImmediateFlush();
    }

    /// <summary>
    ///     Disconnect-path fix for the write-behind-vs-zone-tick-removal race (see
    ///     <c>GameConnectionHost</c>'s own disconnect-cleanup remarks for the full citation): unlike
    ///     <see cref="RequestImmediateFlush" />, which only raises a signal the shared drain loop might not act
    ///     on until well after the zone's own tick has already removed this character from
    ///     <see cref="ZoneRegistry.TryGetPlayer" />'s live registry, this reads and persists the character's
    ///     CURRENT state right now, synchronously. The caller MUST invoke this before posting the zone's own
    ///     Leave command for the same character -- calling it after would reopen the exact race this exists to
    ///     close.
    /// </summary>
    /// <remarks>
    ///     Progress is persisted first, at the character's current <c>FlushSequence</c> -- same "wins the
    ///     shared slot" precedence <see cref="ProgressWriteBehindHost.FlushAsync" /> already gives it against
    ///     the periodic drain. Position is persisted second, at <c>FlushSequence + 1</c> (the same bump-by-one
    ///     technique <see cref="ZoneTransfer.CreateEnterData" /> already uses for a handoff's MapId change) so
    ///     it can never be silently no-op'd by <c>usp_Character_PersistBatch</c>'s "strictly greater than
    ///     stored" idempotence guard, which the Progress write above has just advanced to the same value.
    /// </remarks>
    public async ValueTask FlushCharacterNowAsync(int characterId, CancellationToken ct)
    {
        await _flushGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_zones.TryGetPlayer(characterId, out var state))
            {
                _logger.LogDebug(
                    "FlushCharacterNowAsync({CharacterId}): character not found in any zone's live registry -- nothing to persist",
                    characterId);
                return;
            }

            var progressRow = new CharacterProgressTvp(characterId, state.FlushSequence, state.Level, state.Level2,
                state.Experience, state.Life, state.MaxLife, state.Mana, state.MaxMana, state.StatVit, state.StatStr,
                state.StatInt, state.StatDex, state.StatPoints, state.SkillPoints, state.ContributionPoints,
                state.Exp2, state.RebirthCount, state.EatLifePotion, state.EatManaPotion, state.EatStrPotion,
                state.EatDexPotion, state.EatElePotion, state.DropItemTime, state.M15PetLuckyBoxPity);
            await _characters.PersistProgressAsync([progressRow], ct).ConfigureAwait(false);

            var positionRow = new CharacterPositionTvp(characterId, state.FlushSequence + 1, state.MapId, state.PosX,
                state.PosY, state.PosZ, state.Heading);
            await _characters.PersistPositionsAsync([positionRow], ct).ConfigureAwait(false);
        }
        finally
        {
            _flushGate.Release();
        }
    }

    /// <summary>Idempotent -- safe whether or not <see cref="StopAsync" /> already disposed the flusher.</summary>
    public async ValueTask DisposeAsync()
    {
        await _flusher.DisposeAsync().ConfigureAwait(false);
        _flushGate.Dispose();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _flusher.RunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await _flusher.DisposeAsync().ConfigureAwait(false);
    }
}
