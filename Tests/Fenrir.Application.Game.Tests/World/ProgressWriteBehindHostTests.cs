using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Blocker #1 (2026-07-05 audit) regression coverage: Vitals/Progression were marked dirty throughout combat/
///     quest/guild/pet code but never actually flushed to game.Characters -- the same gap that let
///     TribeActionService's scroll-redemption ContributionPoints debit revert on relog (tSort 16/17 dupe). These
///     tests exercise <see cref="ProgressWriteBehindHost.FlushAsync" /> directly (pure in-memory, fakes only,
///     same posture as <see cref="Fenrir.Data.Tests.WriteBehind.WriteBehindFlusherTests" />).
/// </summary>
public sealed class ProgressWriteBehindHostTests
{
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

    [Fact]
    public async Task FlushAsync_DirtyVitalsProgressionCharacter_PersistsRowWithCurrentFieldValues()
    {
        const int characterId = 10;
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, characterId, out var state);

        state.Level = 55;
        state.Experience = 123_456L;
        state.Life = 700;
        state.MaxLife = 900;
        state.Mana = 300;
        state.MaxMana = 320;
        state.ContributionPoints = 4_200;
        state.SkillPoints = 3;
        state.StatPoints = 7;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals | DirtyFlags.Progression);

        var dirty = dirtyTracker.DrainAll();
        var characters = new FakeCharacterRepository();
        var progress = new ProgressWriteBehindHost(registry, characters);

        var claimed = await progress.FlushAsync(dirty, CancellationToken.None);

        Assert.Contains(characterId, claimed);
        var row = Assert.Single(characters.PersistedProgressRows);
        Assert.Equal(characterId, row.CharacterId);
        Assert.Equal(state.FlushSequence, row.FlushSequence);
        Assert.Equal((short)55, row.Level);
        Assert.Equal(123_456L, row.Experience);
        Assert.Equal(700, row.Life);
        Assert.Equal(900, row.MaxLife);
        Assert.Equal(300, row.Mana);
        Assert.Equal(320, row.MaxMana);
        Assert.Equal(4_200, row.ContributionPoints);
        Assert.Equal(3, row.SkillPoints);
        Assert.Equal(7, row.StatPoints);
    }

    [Fact]
    public async Task FlushAsync_TwoConsecutiveProgressOnlyDrainCycles_WithZeroMovementBetween_BothPersist_AndFlushSequenceStrictlyIncreases()
    {
        const int characterId = 11;
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, characterId, out var state);
        var characters = new FakeCharacterRepository();
        var progress = new ProgressWriteBehindHost(registry, characters);

        // First combat hit: no movement anywhere in this test.
        state.Life -= 50;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        var firstBatch = dirtyTracker.DrainAll();
        await progress.FlushAsync(firstBatch, CancellationToken.None);

        // Second combat hit, still zero movement in between.
        state.Life -= 30;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        var secondBatch = dirtyTracker.DrainAll();
        await progress.FlushAsync(secondBatch, CancellationToken.None);

        Assert.Equal(2, characters.PersistedProgressRows.Count);
        var firstRow = characters.PersistedProgressRows[0];
        var secondRow = characters.PersistedProgressRows[1];

        // The critical guard: usp_Character_PersistProgressBatch only accepts a STRICTLY greater FlushSequence.
        // Without PlayerRuntimeState.MarkProgressDirty bumping FlushSequence on every call (not just HandleMove's
        // move-only cadence), both rows would carry an identical value and the second flush would silently no-op
        // against the real stored procedure's guard.
        Assert.True(secondRow.FlushSequence > firstRow.FlushSequence,
            $"expected second flush's FlushSequence ({secondRow.FlushSequence}) to strictly exceed the first's ({firstRow.FlushSequence})");
        Assert.Equal(750, firstRow.Life); // ZoneTestKit.EnterData's default Life (800) minus the first hit (50)
        Assert.Equal(720, secondRow.Life); // minus the second hit (30) too
    }

    [Fact]
    public async Task FlushAsync_ContributionPointsDebit_SurvivesADrainAndFlushCycle_ClosingTheTribeScrollDupeExploit()
    {
        const int characterId = 12;
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, characterId, out var state);
        state.ContributionPoints = 10_000;

        // Simulate TribeActionService.RedeemScrollAsync's in-memory CP debit for a paid tSort 16/17 redemption.
        state.ContributionPoints -= 2_000;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        var dirty = dirtyTracker.DrainAll();
        var characters = new FakeCharacterRepository();
        var progress = new ProgressWriteBehindHost(registry, characters);

        await progress.FlushAsync(dirty, CancellationToken.None);

        var row = Assert.Single(characters.PersistedProgressRows);
        Assert.Equal(8_000, row.ContributionPoints); // debit reached the DB -- a relog can no longer revert it for free
    }

    [Fact]
    public async Task FlushAsync_CharacterAbsentFromEveryZone_IsSkipped_NotClaimedAndNoRowBuilt()
    {
        const int characterId = 13;
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, characterId, out var state);
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        var dirty = dirtyTracker.DrainAll();

        // The character logged out (or is mid-handoff) between being marked dirty and this flush cycle running.
        Assert.True(registry.TryGet(1, out var zone));
        zone!.Post(ZoneCommand.Leave(characterId));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.False(registry.TryGetPlayer(characterId, out _));

        var characters = new FakeCharacterRepository();
        var progress = new ProgressWriteBehindHost(registry, characters);

        var claimed = await progress.FlushAsync(dirty, CancellationToken.None);

        Assert.Empty(claimed);
        Assert.Empty(characters.PersistedProgressRows);
    }

    [Fact]
    public async Task FlushAsync_EntryWithOnlyPositionFlag_IsIgnored_NotClaimed()
    {
        const int characterId = 14;
        var (registry, dirtyTracker) = CreateRegistryWithOnePlayer(1, characterId, out _);
        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);
        var dirty = dirtyTracker.DrainAll();

        var characters = new FakeCharacterRepository();
        var progress = new ProgressWriteBehindHost(registry, characters);

        var claimed = await progress.FlushAsync(dirty, CancellationToken.None);

        Assert.Empty(claimed);
        Assert.Empty(characters.PersistedProgressRows);
    }
}
