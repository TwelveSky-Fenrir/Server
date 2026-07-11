using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World;

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
    public async Task
        RequestImmediateFlush_CharacterDirtyForBothPositionAndProgress_ProgressPersists_PositionDefersInsteadOfCollidingOnTheSharedFlushSequence()
    {
        const int characterId = 21;
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, characterId, out var state);

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

        Assert.True(await WaitUntilAsync(() => characters.PersistedProgressRows.Count == 1, BoundedWait));
        Assert.Equal(760, characters.PersistedProgressRows[0].Life);

        Assert.Empty(characters.PersistedPositionRows);
        Assert.True(dirtyTracker.Count > 0, "Position should have been re-marked dirty for the next drain cycle");

        host.RequestImmediateFlush();
        Assert.True(await WaitUntilAsync(() => characters.PersistedPositionRows.Count == 1, BoundedWait));
        Assert.Equal(999f, characters.PersistedPositionRows[0].PosX);

        await host.StopAsync(CancellationToken.None);
    }

        [Fact]
    public async Task
        FlushCharacterNowAsync_CharacterLiveInRegistry_PersistsProgressAndPositionWithoutTheBackgroundLoop()
    {
        const int characterId = 30;
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, characterId, out var state);
        var originalFlushSequence = state.FlushSequence;
        state.PosX = 123f;
        state.Life = 700;

        var characters = new FakeCharacterRepository();
        var progress = new ProgressWriteBehindHost(registry, characters);
        await using var host = new PositionWriteBehindHost(registry, dirtyTracker, characters, progress,
            NullLogger<PositionWriteBehindHost>.Instance);

        await host.FlushCharacterNowAsync(characterId, CancellationToken.None);

        var progressRow = Assert.Single(characters.PersistedProgressRows);
        Assert.Equal(characterId, progressRow.CharacterId);
        Assert.Equal(700, progressRow.Life);
        Assert.Equal(originalFlushSequence, progressRow.FlushSequence);

        var positionRow = Assert.Single(characters.PersistedPositionRows);
        Assert.Equal(characterId, positionRow.CharacterId);
        Assert.Equal(123f, positionRow.PosX);

        Assert.Equal(originalFlushSequence + 1, positionRow.FlushSequence);
    }

        [Fact]
    public async Task FlushCharacterNowAsync_CharacterNotInAnyZonesRegistry_IsANoOp()
    {
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, 40, out _);

        var characters = new FakeCharacterRepository();
        var progress = new ProgressWriteBehindHost(registry, characters);
        await using var host = new PositionWriteBehindHost(registry, dirtyTracker, characters, progress,
            NullLogger<PositionWriteBehindHost>.Instance);

        const int missingCharacterId = 999;
        await host.FlushCharacterNowAsync(missingCharacterId, CancellationToken.None);

        Assert.Empty(characters.PersistedProgressRows);
        Assert.Empty(characters.PersistedPositionRows);
    }
}
