using Fenrir.Data.Characters;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.World;

/// <summary>
///     Flushes dirty-tracked character positions to <see cref="CharacterRepository.PersistPositionsAsync" />.
///     Reads CURRENT position from <see cref="ZoneRegistry.TryGetPlayer" /> since the dirty tracker only holds
///     flags, never values. Exposed as <see cref="IWriteBehindFlusher" /> so a disconnecting session can
///     request an immediate, targeted flush.
/// </summary>
public sealed class PositionWriteBehindHost : BackgroundService, IWriteBehindFlusher
{
    private readonly WriteBehindFlusher<int> _flusher;

    public PositionWriteBehindHost(ZoneRegistry zones, DirtyTracker<int> dirtyTracker, ICharacterRepository characters,
        ILogger<PositionWriteBehindHost> logger)
    {
        _flusher = new WriteBehindFlusher<int>(
            dirtyTracker,
            async (dirty, ct) =>
            {
                var rows = new List<CharacterPositionTvp>(dirty.Count);

                foreach (var characterId in dirty.Keys)
                    if (zones.TryGetPlayer(characterId, out var state))
                        rows.Add(new CharacterPositionTvp(characterId, state.FlushSequence, state.MapId, state.PosX,
                            state.PosY, state.PosZ, state.Heading));

                // A player absent from every zone (logged out, or mid-handoff) is correctly dropped here --
                // their last position was already flushed by the disconnect path, and a handoff re-marks
                // them dirty on arrival.
                await characters.PersistPositionsAsync(rows, ct).ConfigureAwait(false);
            },
            onFlushError: ex => logger.LogError(ex, "Position write-behind flush failed"));
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
