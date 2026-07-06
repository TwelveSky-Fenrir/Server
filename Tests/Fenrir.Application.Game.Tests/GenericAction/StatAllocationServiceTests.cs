using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.GenericAction;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.GenericAction;

// CZ_PROCESS_DATA_SEND tSort 206, ProcessForStatPlus -- GenericActionService.AllocateStatPointAsync's own
// orchestration (resolver gate -> attribute credit + stat-point debit -> derived-stat recompute -> mirror onto
// PlayerRuntimeState). Not yet reachable from GenericActionHandler's dispatch switch -- these tests call the
// service method directly, independent of that wiring.
public class StatAllocationServiceTests
{
    private const int CharacterId = 10;
    private const short Level = 42;

    private static (Zone Zone, PlayerRuntimeState State, WorldDataCache WorldData) Setup(int statPoints,
        int statVit = 1, int statStr = 1, int statInt = 1, int statDex = 1)
    {
        var levels = new Dictionary<short, LevelRowDto> { [Level] = WorldDataTestRows.Level(Level) }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(levelsByLevel: levels);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);

        var (session, _) = ZoneTestKit.CreateSession(CharacterId);
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, zone.MapId, level: Level)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.TryGetPlayer(CharacterId, out var state);
        state!.StatPoints = statPoints;
        state.StatVit = statVit;
        state.StatStr = statStr;
        state.StatInt = statInt;
        state.StatDex = statDex;

        return (zone, state, worldData);
    }

    private static GenericActionService CreateService(WorldDataCache worldData)
    {
        return new GenericActionService(new FakeCharacterRepository(), worldData, new QuestCatalog(worldData),
            new PartyRegistry(), NullLogger<GenericActionService>.Instance);
    }

    [Fact]
    public async Task SmallFixedCategory_Succeeds_CreditsStrength_DebitsOnePoint_AndRecomputesStats()
    {
        var (zone, state, worldData) = Setup(statPoints: 5, statStr: 1);
        var service = CreateService(worldData);

        var result = await service.AllocateStatPointAsync(1, 0, zone, state, CharacterId, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(2, after!.StatStr);
        Assert.Equal(4, after.StatPoints);
        Assert.NotNull(after.Stats);
        Assert.True(after.Stats!.Value.MaxLife > 0);
    }

    [Fact]
    public async Task VariableCategory_Succeeds_CreditsVitalityByRequestedAmount_AndDebitsSameAmount()
    {
        var (zone, state, worldData) = Setup(statPoints: 10, statVit: 3);
        var service = CreateService(worldData);

        var result = await service.AllocateStatPointAsync(11, 7, zone, state, CharacterId, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, result.Status);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(10, after!.StatVit);
        Assert.Equal(3, after.StatPoints);
    }

    [Fact]
    public async Task LargeFixedCategory_InsufficientBalance_Aborts_AndDoesNotMutate()
    {
        var (zone, state, worldData) = Setup(statPoints: 4, statInt: 1);
        var service = CreateService(worldData);

        var result = await service.AllocateStatPointAsync(8, 0, zone, state, CharacterId, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, result.Status);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(1, after!.StatInt);
        Assert.Equal(4, after.StatPoints);
        Assert.Null(after.Stats);
    }

    [Fact]
    public async Task CategoryOutsideLegalRange_Aborts_AndDoesNotMutate()
    {
        var (zone, state, worldData) = Setup(statPoints: 1_000_000);
        var service = CreateService(worldData);

        var result = await service.AllocateStatPointAsync(13, 50, zone, state, CharacterId, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, result.Status);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(1_000_000, after!.StatPoints);
        Assert.Null(after.Stats);
    }
}
