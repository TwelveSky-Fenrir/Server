using System.Collections.ObjectModel;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class WorldStateServiceTests
{
    private static (WorldStateService Service, FakeWorldStateRepository Repository) CreateInitialized()
    {
        var repository = new FakeWorldStateRepository();
        var service = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return (service, repository);
    }

    private static (WorldStateService Service, CapturingLogger<WorldStateService> Logger) CreateInitializedWithLogger()
    {
        var logger = new CapturingLogger<WorldStateService>();
        var service = new WorldStateService(new FakeWorldStateRepository(), logger);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return (service, logger);
    }

    [Fact]
    public async Task InitializeAsync_LoadsWorldTribesAndAllianceOffers_FromRepository()
    {
        var repository = new FakeWorldStateRepository
        {
            Row = new WorldStateRowDto(1, 2, 1430, true, 3, 900, 1, 5, DateTime.UtcNow),
            AllianceOffers = [new WorldStateAllianceOfferDto(0, 1, true)]
        };
        repository.Tribes[2] = new WorldStateTribeDto(2, DateTime.UtcNow, false, 42, true);
        var service = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);

        await service.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, repository.EnsureInitializedCallCount);
        var world = service.World;
        Assert.Equal((byte?)2, world.Zone038WinTribe);
        Assert.Equal(1430, world.Zone038WinTribeTime);
        Assert.True(world.TribeSymbolBattle);
        Assert.Equal((byte?)3, world.MonsterSymbol);
        Assert.Equal(900, world.MonsterSymbolEndTime);
        Assert.Equal((byte?)1, world.HighTribe);
        Assert.Equal(5, world.UpdateTribePoint);

        var tribe2 = service.GetTribe(2);
        Assert.Equal(42, tribe2.Points);
        Assert.True(tribe2.IsClosed);
        Assert.False(tribe2.HasSymbol);

        Assert.True(service.TryGetAllianceOffer(0, 1, out var offer));
        Assert.True(offer.IsAccepted);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_Throws()
    {
        var (service, _) = CreateInitialized();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InitializeAsync_NoSingletonRow_ThrowsInvariantViolation()
    {
        // GetAsync returning a null Row simulates EnsureInitializedAsync never having actually run against a
        // real database -- must fail loudly at boot, not silently default to an empty world.
        var repository = new NullRowRepository();
        var service = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public void ReadingState_BeforeInitialize_Throws()
    {
        var repository = new FakeWorldStateRepository();
        var service = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);

        Assert.Throws<InvalidOperationException>(() => service.World);
        Assert.Throws<InvalidOperationException>(() => service.GetTribe(0));
    }

    [Fact]
    public void SetZone038Winner_UpdatesWorldAndMarksDirty()
    {
        var (service, _) = CreateInitialized();

        service.SetZone038Winner(2);

        Assert.Equal((byte?)2, service.World.Zone038WinTribe);
        Assert.NotNull(service.World.Zone038WinTribeTime);
        Assert.True(service.IsDirty);
    }

    [Fact]
    public void StartTribeSymbolBattle_OpensBattle_AndResetsEveryTribeToItsOwnSymbol()
    {
        var (service, _) = CreateInitialized();
        service.ResolveTribeSymbol(1, 2); // tribe 1 loses its own symbol to tribe 2 first

        service.StartTribeSymbolBattle();

        Assert.True(service.World.TribeSymbolBattle);
        Assert.All(service.GetAllTribes(), t => Assert.True(t.HasSymbol));
    }

    [Fact]
    public void EndTribeSymbolBattle_ClosesBattle_WithoutTouchingTribeOwnership()
    {
        var (service, _) = CreateInitialized();
        service.StartTribeSymbolBattle();
        service.ResolveTribeSymbol(1, 2);

        service.EndTribeSymbolBattle();

        Assert.False(service.World.TribeSymbolBattle);
        Assert.False(service.GetTribe(1).HasSymbol); // untouched by EndTribeSymbolBattle itself
    }

    [Fact]
    public void ResolveTribeSymbol_WinnerIsSlotsOwnTribe_KeepsSymbol()
    {
        var (service, _) = CreateInitialized();

        service.ResolveTribeSymbol(1, 1);

        Assert.True(service.GetTribe(1).HasSymbol);
    }

    [Fact]
    public void ResolveTribeSymbol_WinnerIsAnotherTribe_LosesSymbol()
    {
        var (service, _) = CreateInitialized();

        service.ResolveTribeSymbol(1, 3);

        Assert.False(service.GetTribe(1).HasSymbol);
        // The schema can't record who took it -- only that tribe 1 no longer holds its own.
        Assert.True(service.GetTribe(3).HasSymbol);
    }

    [Fact]
    public void ResolveMonsterSymbol_SetsHolderAndEndTime()
    {
        var (service, _) = CreateInitialized();

        service.ResolveMonsterSymbol(2);

        Assert.Equal((byte?)2, service.World.MonsterSymbol);
        Assert.NotNull(service.World.MonsterSymbolEndTime);
    }

    [Fact]
    public void StartTribeSymbolBattle_LogsTheWindowOpening()
    {
        var (service, logger) = CreateInitializedWithLogger();

        service.StartTribeSymbolBattle();

        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Information && e.Message.Contains("opened", StringComparison.Ordinal));
    }

    [Fact]
    public void EndTribeSymbolBattle_LogsTheWindowClosing()
    {
        var (service, logger) = CreateInitializedWithLogger();
        service.StartTribeSymbolBattle();

        service.EndTribeSymbolBattle();

        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Information && e.Message.Contains("closed", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveTribeSymbol_LogsTheSlotAndWinner()
    {
        var (service, logger) = CreateInitializedWithLogger();

        service.ResolveTribeSymbol(1, 3);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information &&
                                              e.Message.Contains("slot 1", StringComparison.Ordinal) &&
                                              e.Message.Contains("winner=3", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveMonsterSymbol_LogsTheWinner()
    {
        var (service, logger) = CreateInitializedWithLogger();

        service.ResolveMonsterSymbol(2);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information &&
                                              e.Message.Contains("winner=2", StringComparison.Ordinal));
    }

    [Fact]
    public void AddTribePoints_AccumulatesAcrossCalls_AndReturnsRunningTotal()
    {
        var (service, _) = CreateInitialized();

        Assert.Equal(10, service.AddTribePoints(0, 10));
        Assert.Equal(25, service.AddTribePoints(0, 15));
        Assert.Equal(25, service.GetTribe(0).Points);
    }

    [Fact]
    public void SetTribePoints_OverwritesAbsoluteValue()
    {
        var (service, _) = CreateInitialized();
        service.AddTribePoints(0, 999);

        service.SetTribePoints(0, 7);

        Assert.Equal(7, service.GetTribe(0).Points);
    }

    [Fact]
    public void SetTribeClosed_TogglesGateFlag()
    {
        var (service, _) = CreateInitialized();

        service.SetTribeClosed(1, true);
        Assert.True(service.GetTribe(1).IsClosed);

        service.SetTribeClosed(1, false);
        Assert.False(service.GetTribe(1).IsClosed);
    }

    [Fact]
    public void SetHighTribe_AcceptsSpecificTribeAndNull()
    {
        var (service, _) = CreateInitialized();

        service.SetHighTribe(2);
        Assert.Equal((byte?)2, service.World.HighTribe);

        service.SetHighTribe(null);
        Assert.Null(service.World.HighTribe);
    }

    [Fact]
    public void SetAllianceOffer_RoundTripsThroughReaders()
    {
        var (service, _) = CreateInitialized();

        service.SetAllianceOffer(0, 1, true);

        Assert.True(service.TryGetAllianceOffer(0, 1, out var offer));
        Assert.True(offer.IsAccepted);
        Assert.Contains(service.GetAllianceOffers(), o => o is { FromTribeId: 0, ToTribeId: 1, IsAccepted: true });
    }

    [Fact]
    public void SetAllianceOffer_TargetingSelf_Throws()
    {
        var (service, _) = CreateInitialized();

        Assert.Throws<ArgumentException>(() => service.SetAllianceOffer(1, 1, true));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(200)]
    public void MutationWithOutOfRangeTribeId_Throws(byte invalidTribeId)
    {
        var (service, _) = CreateInitialized();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.SetZone038Winner(invalidTribeId));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.SetTribePoints(invalidTribeId, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetTribe(invalidTribeId));
    }

    [Fact]
    public async Task FlushIfDirtyAsync_WhenNothingChanged_NeverCallsRepository()
    {
        var (service, repository) = CreateInitialized();

        await service.FlushIfDirtyAsync(CancellationToken.None);

        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Empty(repository.TribeUpdateCalls);
    }

    [Fact]
    public async Task FlushIfDirtyAsync_WhenDirty_PersistsWorldAndAllFourTribesAndOffers_ThenClearsDirty()
    {
        var (service, repository) = CreateInitialized();
        service.SetZone038Winner(2);
        service.AddTribePoints(1, 30);
        service.SetAllianceOffer(0, 3, true);

        await service.FlushIfDirtyAsync(CancellationToken.None);

        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal((byte?)2, repository.LastWorldUpdate!.Value.Zone038WinTribe);
        Assert.Equal(4, repository.TribeUpdateCalls.Count); // every tribe re-persisted, matching the legacy's blind rewrite
        Assert.Contains(repository.TribeUpdateCalls, c => c.TribeId == 1 && c.Points == 30);
        Assert.Contains(repository.AllianceOfferCalls, c => c is { From: 0, To: 3, IsAccepted: true });
        Assert.False(service.IsDirty);
    }

    [Fact]
    public async Task FlushIfDirtyAsync_OnRepositoryFailure_LeavesDirty_ForNextIntervalToRetry()
    {
        var (service, repository) = CreateInitialized();
        service.SetZone038Winner(2);
        repository.ThrowOnUpdate = true;

        await service.FlushIfDirtyAsync(CancellationToken.None); // must not throw

        Assert.True(service.IsDirty);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task GetTribeVotesAsync_DelegatesToRepositoryForTheRequestedTribe()
    {
        var (service, repository) = CreateInitialized();
        repository.VotesByTribe[2] = [new TribeVoteDto(2, 0, 555, 40, 3, 100, DateTime.UtcNow)];

        var votes = await service.GetTribeVotesAsync(2, CancellationToken.None);

        Assert.Single(votes);
        Assert.Equal(555, votes[0].CandidateCharacterId);
    }

    [Fact]
    public void AddTribePoints_UnderConcurrentCallers_NeverLosesAnUpdate()
    {
        var (service, _) = CreateInitialized();
        const int callsPerThread = 200;
        const int threads = 8;

        Parallel.For(0, threads, _ =>
        {
            for (var i = 0; i < callsPerThread; i++)
                service.AddTribePoints(0, 1);
        });

        Assert.Equal(threads * callsPerThread, service.GetTribe(0).Points);
    }

    private sealed class NullRowRepository : IWorldStateRepository
    {
        public ValueTask EnsureInitializedAsync(CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<(WorldStateRowDto? Row, ReadOnlyCollection<WorldStateTribeDto> Tribes,
                ReadOnlyCollection<WorldStateAllianceOfferDto> AllianceOffers)>
            GetAsync(CancellationToken ct)
        {
            return ValueTask.FromResult<(WorldStateRowDto?, ReadOnlyCollection<WorldStateTribeDto>,
                ReadOnlyCollection<WorldStateAllianceOfferDto>)>(
                (null, new ReadOnlyCollection<WorldStateTribeDto>([]),
                    new ReadOnlyCollection<WorldStateAllianceOfferDto>([])));
        }

        public ValueTask UpdateAsync(byte? zone038WinTribe, int? zone038WinTribeTime, bool tribeSymbolBattle,
            byte? monsterSymbol, int? monsterSymbolEndTime, byte? highTribe, short updateTribePoint,
            CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask UpdateTribeAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, int points,
            bool isClosed, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask SetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted,
            CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct) =>
            ValueTask.FromResult(new ReadOnlyCollection<TribeVoteDto>([]));

        public ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
            short candidateLevel, int killOtherTribeCount, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask AddTribeVotePointsAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct) =>
            ValueTask.CompletedTask;

        public ValueTask ClearTribeVotesAsync(byte tribeId, CancellationToken ct) => ValueTask.CompletedTask;
    }
}
