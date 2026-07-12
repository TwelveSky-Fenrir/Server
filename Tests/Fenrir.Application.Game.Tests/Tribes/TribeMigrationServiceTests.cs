using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Tribes;

public class TribeMigrationServiceTests
{
    private const int CharacterId = 500;

    private static readonly TimeProvider SaturdayWithinWindow =
        new FixedTimeProvider(new DateTimeOffset(2024, 1, 6, 17, 0, 0, TimeSpan.Zero));

    private static async Task<T> RunToCompletionAsync<T>(ValueTask<T> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("TribeMigrationService task never completed.");
        }

        return await task;
    }

    private static QuestCatalog CatalogWith(params QuestRowDto[] quests)
    {
        var rows = WorldDataTestRows.MinimalRows() with { Quests = quests, QuestRewards = [], QuestSpeeches = [] };
        var (cache, _) = WorldDataCacheBuilder.Build(rows);
        return new QuestCatalog(cache);
    }

    private static FakeWorldStateRepository WithTribePoints(int t0, int t1, int t2, int t3)
    {
        return new FakeWorldStateRepository
        {
            Tribes =
            [
                new WorldStateTribeDto(0, null, true, t0, false),
                new WorldStateTribeDto(1, null, true, t1, false),
                new WorldStateTribeDto(2, null, true, t2, false),
                new WorldStateTribeDto(3, null, true, t3, false)
            ]
        };
    }

    private static (Zone Zone, PlayerRuntimeState State, WorldStateService WorldState) SetUp(
        GameServerOptions options, FakeWorldStateRepository repository, byte tribe, short level = 145)
    {
        var worldState = ZoneTestKit.CreateWorldState(repository);
        var zone = ZoneTestKit.CreateZone(1, options, worldState: worldState);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(CharacterId,
            ZoneTestKit.EnterData(session, zone.MapId, tribe: tribe, level: level)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (zone, state!, worldState);
    }

    private static TribeMigrationService CreateService(FakeCharacterRepository characters,
        WorldStateService worldState, GameServerOptions options, QuestCatalog? catalog = null,
        TimeProvider? timeProvider = null)
    {
        return new TribeMigrationService(characters, worldState, catalog ?? CatalogWith(),
            Options.Create(options), timeProvider ?? SaturdayWithinWindow,
            NullLogger<TribeMigrationService>.Instance);
    }

    [Fact]
    public async Task ConvertAsync_OutboundHappyPath_MovesToTribeThreeAndResetsQuestsOnly()
    {
        var options = new GameServerOptions { TribeFourConversionEnabled = true };
        var (zone, state, worldState) = SetUp(options, WithTribePoints(200, 50, 100, 0), 0);
        state.QuestStepPermanent = 7;
        state.QuestActiveFlag = 1;
        state.QuestSort = 2;
        state.QuestTargetPhase = 3;
        state.QuestKillCounter = 4;

        state.Experience = 12_345;
        state.StatPoints = 9;
        state.Level = 200;
        var (money, statPoints, level, experience) = (state.Experience, state.StatPoints, state.Level,
            state.Experience);

        var characters = new FakeCharacterRepository();
        var service = CreateService(characters, worldState, options);

        var outcome = await RunToCompletionAsync(service.ConvertAsync(zone, state, CharacterId, CancellationToken.None),
            zone);

        Assert.Equal(TribeMigrationOutcome.Success, outcome);
        Assert.Equal((byte)3, state.Tribe);
        Assert.Equal(0, state.QuestStepPermanent);
        Assert.Equal(0, state.QuestActiveFlag);
        Assert.Equal(0, state.QuestSort);
        Assert.Equal(0, state.QuestTargetPhase);
        Assert.Equal(0, state.QuestKillCounter);

        var persisted = Assert.Single(characters.TribeFourConversions);
        Assert.Equal((CharacterId, (byte)3, 0, 0, 0, 0, 0), persisted);
        Assert.True(characters.LastConsumeSharedQuota);

        Assert.Equal(experience, state.Experience);
        Assert.Equal(statPoints, state.StatPoints);
        Assert.Equal(level, state.Level);
        Assert.Equal(money, state.Experience);
    }

    [Fact]
    public async Task ConvertAsync_ReturnHappyPath_RestoresTribeAndTerminalQuestStepAndDecrementsAllowance()
    {
        var options = new GameServerOptions { TribeFourConversionEnabled = true };
        var (zone, state, worldState) = SetUp(options, WithTribePoints(0, 50, 0, 60), 3);
        state.PreviousTribe = 1;
        state.TribeFourReturnAllowance = 1;

        var stepOne = WorldDataTestRows.Quest(1) with { Category = 2, Step = 1 };
        var stepTwo = WorldDataTestRows.Quest(2) with { Category = 2, Step = 2 };
        var catalog = CatalogWith(stepOne, stepTwo);

        var characters = new FakeCharacterRepository();
        var service = CreateService(characters, worldState, options, catalog);

        var outcome = await RunToCompletionAsync(service.ConvertAsync(zone, state, CharacterId, CancellationToken.None),
            zone);

        Assert.Equal(TribeMigrationOutcome.Success, outcome);
        Assert.Equal((byte)1, state.Tribe);
        Assert.Equal(2, state.QuestStepPermanent);
        Assert.Equal(0, state.QuestActiveFlag);
        Assert.Equal(0, state.TribeFourReturnAllowance);

        var persisted = Assert.Single(characters.TribeFourConversions);
        Assert.Equal((CharacterId, (byte)1, 2, 0, 0, 0, 0), persisted);
        Assert.True(characters.LastConsumeSharedQuota);
    }

    [Fact]
    public async Task ConvertAsync_QuotaExhausted_NoStateMutationAndOutcomeReported()
    {
        var options = new GameServerOptions { TribeFourConversionEnabled = true };
        var (zone, state, worldState) = SetUp(options, WithTribePoints(200, 50, 100, 0), 0);
        var characters = new FakeCharacterRepository { ThrowQuotaExhausted = true };
        var service = CreateService(characters, worldState, options);

        var outcome = await RunToCompletionAsync(service.ConvertAsync(zone, state, CharacterId, CancellationToken.None),
            zone);

        Assert.Equal(TribeMigrationOutcome.QuotaExhausted, outcome);
        Assert.Equal((byte)0, state.Tribe);
        Assert.Empty(characters.TribeFourConversions);
    }

    [Fact]
    public async Task ConvertAsync_IneligibleCharacter_NeverConsumesTheSharedQuota()
    {
        var options = new GameServerOptions { TribeFourConversionEnabled = true };
        var (zone, state, worldState) = SetUp(options, WithTribePoints(200, 50, 100, 0), 0, 1);
        var characters = new FakeCharacterRepository();
        var service = CreateService(characters, worldState, options);

        var outcome = await RunToCompletionAsync(service.ConvertAsync(zone, state, CharacterId, CancellationToken.None),
            zone);

        Assert.Equal(TribeMigrationOutcome.LevelTooLow, outcome);
        Assert.Null(characters.LastConsumeSharedQuota);
        Assert.Empty(characters.TribeFourConversions);
    }

    [Fact]
    public async Task ConvertAsync_FeatureDisabled_NeverConsumesTheSharedQuotaOrMutatesState()
    {
        var options = new GameServerOptions { TribeFourConversionEnabled = false };
        var (zone, state, worldState) = SetUp(options, WithTribePoints(200, 50, 100, 0), 0);
        var characters = new FakeCharacterRepository();
        var service = CreateService(characters, worldState, options);

        var outcome = await RunToCompletionAsync(service.ConvertAsync(zone, state, CharacterId, CancellationToken.None),
            zone);

        Assert.Equal(TribeMigrationOutcome.FeatureDisabled, outcome);
        Assert.Null(characters.LastConsumeSharedQuota);
        Assert.Empty(characters.TribeFourConversions);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
