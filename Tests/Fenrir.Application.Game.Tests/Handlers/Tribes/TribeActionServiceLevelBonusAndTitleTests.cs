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


    [Fact]
    public async Task ClaimLevelBonus_NoMilestoneArmed_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId);
        Assert.Equal(0, state.BonusItemLevel);
        var service = CreateService();

        var outcome = await service.ClaimLevelBonusAsync(zone, state, CharacterId, CancellationToken.None);

        Assert.True(outcome.Aborted);
    }

    [Fact]
    public async Task ClaimLevelBonus_UnrecognizedStoredLevel_Aborts_WithoutGrantingAnything()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId);
        state.BonusItemLevel = 999;
        var service = CreateService();

        var outcome = await service.ClaimLevelBonusAsync(zone, state, CharacterId, CancellationToken.None);

        Assert.True(outcome.Aborted);
        Assert.Equal(999, state.BonusItemLevel);
    }

    [Fact]
    public async Task ClaimLevelBonus_MTierLevel132_GrantsTheResolvedDrops_AndClearsTheArmedState()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId);
        state.BonusItemLevel = LevelMilestoneBonus.LvM20;
        state.BonusItemValue = true;
        var service = CreateService();

        var outcome = await service.ClaimLevelBonusAsync(zone, state, CharacterId, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(outcome.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.BonusItemLevel);
        Assert.False(after.BonusItemValue);
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
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(outcome.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.BonusItemLevel);
        Assert.False(after.BonusItemValue);
    }

    [Fact]
    public async Task ClaimLevelBonus_Tier145_StampsExactlyOneUnitOfThePreviousTribeItem_NotTwenty()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId);
        state.BonusItemLevel = 145;
        state.BonusItemValue = true;
        state.PreviousTribe = 0;
        var service = CreateService();

        var outcome = await service.ClaimLevelBonusAsync(zone, state, CharacterId, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(outcome.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(0, after!.BonusItemLevel);
        Assert.False(after.BonusItemValue);
    }


    [Fact]
    public async Task PurchaseTitle_Rank0_CostsExactlyTheFirstTitleContributionCostTableEntry()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId, contributionPoints: TitleContributionCost.CostTable[0]);
        state.Title = 0;
        var service = CreateService();
        var data = new byte[100];
        new TribeWorkTitlePayload { TitleSort = 1, TitleLv = 0 }.Write(data);

        var outcome = await service.PurchaseTitleAsync(zone, state, CharacterId, data, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(outcome.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
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
        Assert.Equal(TitleContributionCost.CostTable[0] - 1, state.ContributionPoints);
    }
}
