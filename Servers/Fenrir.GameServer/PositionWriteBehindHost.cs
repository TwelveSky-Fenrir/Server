using Fenrir.Application.Game.World;
using Fenrir.Data.Characters;
using Fenrir.Data.WriteBehind;

namespace Fenrir.GameServer;

/// <summary>
///     Wires the zones' per-move <c>DirtyTracker&lt;int&gt;.MarkDirty</c> calls (architecture reference
///     §10.5) to <see cref="CharacterRepository.PersistPositionsAsync" />. The flush callback reads CURRENT
///     position from <see cref="ZoneRegistry.TryGetPlayer" /> (a character lives in at most one zone,
///     ADR-0012) rather than from the dirty-tracker itself — the tracker only ever holds flags, never values,
///     by design (Fenrir.Data.WriteBehind.DirtyTracker's own doc comment).
///     Exposed as <see cref="IWriteBehindFlusher" /> so a disconnecting session (GameServer's
///     <c>ZoneConnectionHost</c>) can request an immediate, targeted flush (§10.5: "Flush immediat... sur
///     deconnexion") without depending on the closed generic <see cref="WriteBehindFlusher{TKey}" /> type.
/// </summary>
public sealed class PositionWriteBehindHost : BackgroundService, IWriteBehindFlusher
{
    private readonly WriteBehindFlusher<int> _flusher;

    public PositionWriteBehindHost(ZoneRegistry zones, DirtyTracker<int> dirtyTracker, CharacterRepository characters,
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

                // A player who logged out (or is mid-handoff) between MarkDirty and this flush is simply absent
                // from every zone now -- dropping their row here is correct, not a bug: a logout's last position
                // was already flushed by the immediate, targeted flush the disconnect path itself requests (see
                // this type's class doc), and a handoff re-marks the character dirty when the target zone's
                // Enter lands, so the next periodic flush picks the new map up (ZoneRegistry.TryGetPlayer's doc).
                await characters.PersistPositionsAsync(rows, ct).ConfigureAwait(false);
            },
            onFlushError: ex => logger.LogError(ex, "Position write-behind flush failed"));
    }

    public void RequestImmediateFlush()
    {
        _flusher.RequestImmediateFlush();
    }

    /// <summary>
    ///     Satisfies <see cref="IWriteBehindFlusher" />'s <see cref="IAsyncDisposable" /> requirement.
    ///     <see cref="WriteBehindFlusher{TKey}.DisposeAsync" /> is idempotent, so this is safe to run whether or
    ///     not <see cref="StopAsync" /> already disposed it during a graceful host shutdown.
    /// </summary>
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
