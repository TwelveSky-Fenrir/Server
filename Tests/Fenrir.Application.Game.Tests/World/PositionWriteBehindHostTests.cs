using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     End-to-end coverage of the merged write-behind loop: <see cref="PositionWriteBehindHost" /> is the sole
///     owner of the one <see cref="WriteBehindFlusher{TKey}" /> draining the shared <see cref="DirtyTracker{TKey}" />,
///     and delegates the Vitals/Progression side of every drained batch to <see cref="ProgressWriteBehindHost" />.
/// </summary>
public sealed class PositionWriteBehindHostTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    private static (ZoneRegistry Registry, DirtyTracker<int> DirtyTracker) CreateRegistryWithOnePlayer(
        short mapId, int characterId, out PlayerRuntimeState state)
    {
        var options = ZoneTestKit.Options();
        var dirtyTracker = new DirtyTracker<int>();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            dirtyTracker, NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize([mapId]);

        var (session, _) = ZoneTestKit.CreateSession(characterId);
        registry[mapId].Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId)));
        registry[mapId].Tick(TimeSpan.FromMilliseconds(50));

        if (!registry.TryGetPlayer(characterId, out var resolved))
            throw new InvalidOperationException("Test setup failed to register the player.");

        state = resolved;
        return (registry, dirtyTracker);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            if (condition())
                return true;
            await Task.Delay(10);
        }

        return condition();
    }

    [Fact]
    public async Task RequestImmediateFlush_DirtyPositionOnlyCharacter_PersistsPositionRow()
    {
        const int characterId = 20;
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, characterId, out var state);
        state.PosX = 555f;
        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        var characters = new FakeCharacterRepository();
        var progress = new ProgressWriteBehindHost(registry, characters);
        await using var host = new PositionWriteBehindHost(registry, dirtyTracker, characters, progress,
            NullLogger<PositionWriteBehindHost>.Instance);

        await host.StartAsync(CancellationToken.None);
        host.RequestImmediateFlush();

        Assert.True(await WaitUntilAsync(() => characters.PersistedPositionRows.Count == 1, BoundedWait));
        Assert.Equal(555f, characters.PersistedPositionRows[0].PosX);
        Assert.Empty(characters.PersistedProgressRows);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RequestImmediateFlush_CharacterDirtyForBothPositionAndProgress_ProgressPersists_PositionDefersInsteadOfCollidingOnTheSharedFlushSequence()
    {
        const int characterId = 21;
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, characterId, out var state);

        // Simulate HandleMove's own move-then-mark sequence, then a same-cycle combat hit -- both land in the
        // SAME drained batch, both wanting the shared per-character FlushSequence slot.
        state.PosX = 999f;
        state.FlushSequence++;
        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        state.Life -= 40;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        var characters = new FakeCharacterRepository();
        var progress = new ProgressWriteBehindHost(registry, characters);
        await using var host = new PositionWriteBehindHost(registry, dirtyTracker, characters, progress,
            NullLogger<PositionWriteBehindHost>.Instance);

        await host.StartAsync(CancellationToken.None);
        host.RequestImmediateFlush();

        // Progress must win this cycle -- it is the exploit/durability-critical side.
        Assert.True(await WaitUntilAsync(() => characters.PersistedProgressRows.Count == 1, BoundedWait));
        Assert.Equal(760, characters.PersistedProgressRows[0].Life); // 800 default - 40

        // Position must NOT have been persisted with the identical FlushSequence value this same cycle (that
        // would have silently no-op'd against usp_Character_PersistBatch's real "> " guard) -- instead it was
        // re-marked dirty so a later drain (or the player's next move) picks it up.
        Assert.Empty(characters.PersistedPositionRows);
        Assert.True(dirtyTracker.Count > 0, "Position should have been re-marked dirty for the next drain cycle");

        // That next cycle (still no further movement) now succeeds, since nothing else claims the slot this time.
        host.RequestImmediateFlush();
        Assert.True(await WaitUntilAsync(() => characters.PersistedPositionRows.Count == 1, BoundedWait));
        Assert.Equal(999f, characters.PersistedPositionRows[0].PosX);

        await host.StopAsync(CancellationToken.None);
    }
}
