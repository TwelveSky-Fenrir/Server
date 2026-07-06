using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

/// <summary>
///     Flushes dirty-tracked character positions to <see cref="CharacterRepository.PersistPositionsAsync" />,
///     AND (see <see cref="ProgressWriteBehindHost" />'s remarks for why this is the one and only consumer of
///     the shared <see cref="DirtyTracker{TKey}" />) the Vitals/Progression side of the SAME drained batch via
///     <see cref="ProgressWriteBehindHost.FlushAsync" />. Reads CURRENT state from
///     <see cref="ZoneRegistry.TryGetPlayer" /> since the dirty tracker only holds flags, never values. Exposed
///     as <see cref="IWriteBehindFlusher" /> so a disconnecting session can request an immediate, targeted
///     flush of BOTH position and progress in one call.
/// </summary>
public sealed class PositionWriteBehindHost : BackgroundService, IWriteBehindFlusher
{
    private readonly WriteBehindFlusher<int> _flusher;

    public PositionWriteBehindHost(ZoneRegistry zones, DirtyTracker<int> dirtyTracker, ICharacterRepository characters,
        ProgressWriteBehindHost progress, ILogger<PositionWriteBehindHost> logger)
    {
        _flusher = new WriteBehindFlusher<int>(
            dirtyTracker,
            async (dirty, ct) =>
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
                        // (self-heals on the player's next move, or is caught by the disconnect immediate flush)
                        // -- Progression is the exploit/durability-critical side and must never lose its turn.
                        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);
                        continue;
                    }

                    if (zones.TryGetPlayer(characterId, out var state))
                        rows.Add(new CharacterPositionTvp(characterId, state.FlushSequence, state.MapId, state.PosX,
                            state.PosY, state.PosZ, state.Heading));
                }

                // A player absent from every zone (logged out, or mid-handoff) is correctly dropped here --
                // their last position was already flushed by the disconnect path, and a handoff re-marks
                // them dirty on arrival.
                await characters.PersistPositionsAsync(rows, ct).ConfigureAwait(false);
            },
            onFlushError: ex => logger.LogError(ex, "Character write-behind flush failed (position and/or progress)"));
    }

    public void RequestImmediateFlush()
    {
        _flusher.RequestImmediateFlush();
    }

    /// <summary>Idempotent -- safe whether or not <see cref="StopAsync" /> already disposed the flusher.</summary>
    public async ValueTask DisposeAsync()
    {
        await _flusher.DisposeAsync().ConfigureAwait(false);
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
