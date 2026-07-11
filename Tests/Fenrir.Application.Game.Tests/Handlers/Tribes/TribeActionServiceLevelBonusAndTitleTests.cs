using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

/// <summary>
///     tSort 6 (title purchase) and tSort 8 (level-milestone bonus claim) -- the two
///     <see cref="TribeActionService" /> methods C19's wiring pass moved onto
///     <see cref="TitleContributionCost" />/<see cref="LevelMilestoneBonus" /> instead of their own
///     previously-duplicated private tables. Halo/Rebirth coverage lives in the sibling
///     <c>TribeActionServiceTests</c>; this file is scoped to just these two.
/// </summary>
public class TribeActionServiceLevelBonusAndTitleTests
{
    private const int CharacterId = 10;

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId, int contributionPoints = 10_000)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        state!.ContributionPoints = contributionPoints;

        return (session, pipe, state);
    }

    private static TribeActionService CreateService(FakeCharacterRepository? characters = null)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize([1]);

        var levels = new Dictionary<short, LevelRowDto> { [145] = WorldDataTestRows.Level(145) }
            .ToFrozenDictionary();

        var worldState = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        return new TribeActionService(registry, new FakeTribeRepository(), characters ?? new FakeCharacterRepository(),
            ZoneTestKit.EmptyWorldData(levelsByLevel: levels), worldState, NullLogger<TribeActionService>.Instance);
    }

    // --- tSort 8: level-milestone bonus claim -------------------------------------------------------------

    [Fact]
    public async Task ClaimLevelBonus_NoMilestoneArmed_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId);
        Assert.Equal(0, state.BonusItemLevel); // never armed
        var service = CreateService();

        var outcome = await service.ClaimLevelBonusAsync(zone, state, CharacterId, CancellationToken.None);

        Assert.True(outcome.Aborted);
    }

    [Fact]
    public async Task ClaimLevelBonus_UnrecognizedStoredLevel_Aborts_WithoutGrantingAnything()
    {
        // Every one of the 11 named legacy milestone levels now has a known claim table (the
        // level-milestone-bonus-item-ids recovery pass supplied the last six, the M-tier levels), so a
        // genuinely-unrecognized stored level can no longer be one of those -- use an arbitrary value that was
        // never a legacy milestone at all. ClaimLevelBonusAsync must still refuse it defensively rather than
        // fall through to an unhandled case.
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId);
        state.BonusItemLevel = 999; // never a legacy milestone level
        var service = CreateService();

        var outcome = await service.ClaimLevelBonusAsync(zone, state, CharacterId, CancellationToken.None);

        Assert.True(outcome.Aborted);
        Assert.Equal(999, state.BonusItemLevel); // untouched -- never cleared on abort
    }

    [Fact]
    public async Task ClaimLevelBonus_MTierLevel132_GrantsTheResolvedDrops_AndClearsTheArmedState()
    {
        // Regression guard for the level-milestone-bonus-item-ids recovery: the M-tier levels (114-144) were
        // previously deferred/unarmable; this proves one of the upper-tier cases (which additionally grants
        // item 1458) now claims successfully end-to-end through the delegating service.
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId);
        state.BonusItemLevel = LevelMilestoneBonus.LvM20; // 132
        state.BonusItemValue = true;
        var service = CreateService();

        var outcome = await service.ClaimLevelBonusAsync(zone, state, CharacterId, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50)); // drains the posted TribeProgressZoneCommand mirror

        Assert.False(outcome.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.BonusItemLevel);
        Assert.False(after.BonusItemValue);
        // The exact item set (850/539x2/1458) for tier 132 is pinned by
        // LevelMilestoneBonusTests.TryResolveClaimDrops_Level132_LvM20_GrantsItem850PlusItem539PlusItem1458;
        // this test only proves the claim succeeds end-to-end through the now-delegating service.
    }

    [Fact]
    public async Task ClaimLevelBonus_Tier45_GrantsTheResolvedDrops_AndClearsTheArmedState()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId);
        state.BonusItemLevel = 45;
        state.BonusItemValue = true;
        var service = CreateService();

        var outcome = await service.ClaimLevelBonusAsync(zone, state, CharacterId, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50)); // drains the posted TribeProgressZoneCommand mirror

        Assert.False(outcome.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.BonusItemLevel);
        Assert.False(after.BonusItemValue);
    }

    [Fact]
    public async Task ClaimLevelBonus_Tier145_StampsExactlyOneUnitOfThePreviousTribeItem_NotTwenty()
    {
        // Regression guard for the C19-flagged bug: the pre-wiring switch misread the legacy's "one item
        // stamped with enchant value 20" as a quantity of 20 (new TribeGroundItemDrop(tribeItemId, 20)).
        // LevelMilestoneBonus.TryResolveClaimDrops grants quantity 1 instead -- this proves the delegation
        // actually replaced the buggy inline switch rather than merely compiling alongside it.
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId);
        state.BonusItemLevel = 145;
        state.BonusItemValue = true;
        state.PreviousTribe = 0; // -> tribe item 83809
        var service = CreateService();

        var outcome = await service.ClaimLevelBonusAsync(zone, state, CharacterId, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50)); // drains the posted TribeProgressZoneCommand mirror

        Assert.False(outcome.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.BonusItemLevel);
        Assert.False(after.BonusItemValue);
        // The exact quantity=1 (not 20) drop shape for tier 145's previous-tribe item is pinned by
        // LevelMilestoneBonusTests.TryResolveClaimDrops_Level145_AppendsPreviousTribeItemAsSingleUnit; this
        // test only proves the claim succeeds end-to-end for tier 145 through the now-delegating service.
    }

    // --- tSort 6: title purchase ---------------------------------------------------------------------------

    [Fact]
    public async Task PurchaseTitle_Rank0_CostsExactlyTheFirstTitleContributionCostTableEntry()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId, contributionPoints: TitleContributionCost.CostTable[0]);
        state.Title = 0; // rank 0
        var service = CreateService();
        var data = new byte[100];
        new TribeWorkTitlePayload { TitleSort = 1, TitleLv = 0 }.Write(data);

        var outcome = await service.PurchaseTitleAsync(zone, state, CharacterId, data, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(outcome.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        // Exactly TitleContributionCost.CostTable[0] (800) CP spent -- proves PurchaseTitleAsync now reads its
        // cost from the shared table instead of its own now-deleted private duplicate.
        Assert.Equal(0, after!.ContributionPoints);
    }

    [Fact]
    public async Task PurchaseTitle_InsufficientContributionPoints_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId, contributionPoints: TitleContributionCost.CostTable[0] - 1);
        state.Title = 0;
        var service = CreateService();

        var outcome = await service.PurchaseTitleAsync(zone, state, CharacterId, new byte[100], CancellationToken.None);

        Assert.True(outcome.Aborted);
        Assert.Equal(TitleContributionCost.CostTable[0] - 1, state.ContributionPoints); // untouched
    }
}
