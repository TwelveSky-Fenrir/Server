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
        service.ResolveTribeSymbol(1, 2);

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
        Assert.False(service.GetTribe(1).HasSymbol);
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

    [Fact]
    public void GetAllyOf_NoAcceptedOfferInvolvingTheTribe_ReturnsNull()
    {
        var (service, _) = CreateInitialized();
        service.SetAllianceOffer(0, 1, false);

        Assert.Null(service.GetAllyOf(0));
        Assert.Null(service.GetAllyOf(1));
    }

    [Fact]
    public void GetAllyOf_AcceptedOffer_ReturnsTheOtherTribe_FromEitherDirection()
    {
        var (service, _) = CreateInitialized();
        service.SetAllianceOffer(0, 2, true);

        Assert.Equal((byte?)2, service.GetAllyOf(0));
        Assert.Equal((byte?)0, service.GetAllyOf(2));
    }

    [Fact]
    public void GetAllyOf_NeverReturnsTheTribeItselfEvenIfAskedForItsOwnOffer()
    {
        var (service, _) = CreateInitialized();
        service.SetAllianceOffer(0, 2, true);

        Assert.NotEqual((byte?)0, service.GetAllyOf(0));
        Assert.NotEqual((byte?)2, service.GetAllyOf(2));
    }

    [Fact]
    public void GetAllyOf_TribeInNoActivePair_ReturnsNull_EvenWhenOtherPairsAreActive()
    {
        var (service, _) = CreateInitialized();
        service.SetAllianceOffer(0, 2, true);

        Assert.Null(service.GetAllyOf(1));
        Assert.Null(service.GetAllyOf(3));
    }

    [Fact]
    public void GetAllyOf_OutOfRangeTribeId_Throws()
    {
        var (service, _) = CreateInitialized();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetAllyOf(4));
    }

    [Fact]
    public void GetAllyOf_BeforeInitialize_Throws()
    {
        var repository = new FakeWorldStateRepository();
        var service = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);

        Assert.Throws<InvalidOperationException>(() => service.GetAllyOf(0));
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
        Assert.Empty(repository.SymbolStateUpdateCalls);
        Assert.Empty(repository.TribePointsDeltaCalls);
    }

    [Fact]
    public async Task FlushIfDirtyAsync_WhenDirty_PersistsWorldAndAllFourTribeSymbolStatesAndOffers_ThenClearsDirty()
    {
        var (service, repository) = CreateInitialized();
        service.SetZone038Winner(2);
        service.SetAllianceOffer(0, 3, true);

        await service.FlushIfDirtyAsync(CancellationToken.None);

        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal((byte?)2, repository.LastWorldUpdate!.Value.Zone038WinTribe);
        Assert.Equal(4, repository.SymbolStateUpdateCalls.Count);
        Assert.Empty(repository.TribeUpdateCalls);
        Assert.Contains(repository.AllianceOfferCalls, c => c is { From: 0, To: 3, IsAccepted: true });
        Assert.False(service.IsDirty);
    }

    [Fact]
    public async Task
        FlushIfDirtyAsync_WhenPointsAreDirty_CallsAddTribePointsAsync_WithTheSummedDelta_NotTheAbsoluteValue()
    {
        var (service, repository) = CreateInitialized();
        service.AddTribePoints(1, 10);
        service.AddTribePoints(1, 20);

        await service.FlushIfDirtyAsync(CancellationToken.None);

        Assert.Single(repository.TribePointsDeltaCalls, c => c.TribeId == 1 && c.Delta == 30);
        Assert.False(service.IsDirty);
    }

    [Fact]
    public async Task FlushIfDirtyAsync_TribesWithNoPendingDelta_NeverCallAddTribePointsAsync()
    {
        var (service, repository) = CreateInitialized();
        service.AddTribePoints(1, 10);

        await service.FlushIfDirtyAsync(CancellationToken.None);

        Assert.Single(repository.TribePointsDeltaCalls);
        Assert.DoesNotContain(repository.TribePointsDeltaCalls, c => c.TribeId != 1);
    }

    [Fact]
    public async Task FlushIfDirtyAsync_OnRepositoryFailure_LeavesDirty_ForNextIntervalToRetry()
    {
        var (service, repository) = CreateInitialized();
        service.SetZone038Winner(2);
        repository.ThrowOnUpdate = true;

        await service.FlushIfDirtyAsync(CancellationToken.None);

        Assert.True(service.IsDirty);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task FlushIfDirtyAsync_PointDeltaFlushFailure_CompensatesTheExactAmount_ForNextIntervalToRetry()
    {
        var (service, repository) = CreateInitialized();
        service.AddTribePoints(2, 42);
        repository.ThrowOnAddTribePoints = true;

        await service.FlushIfDirtyAsync(CancellationToken.None);

        Assert.True(service.IsDirty);

        repository.ThrowOnAddTribePoints = false;
        await service.FlushIfDirtyAsync(CancellationToken.None);

        Assert.Single(repository.TribePointsDeltaCalls, c => c.TribeId == 2 && c.Delta == 42);
    }

    [Fact]
    public async Task SetTribePoints_ThenFlush_ReachesTheDbViaTheAdditiveDeltaPath_NotTheAbsoluteOverwrite()
    {
        var (service, repository) = CreateInitialized();

        service.SetTribePoints(0, 500);
        await service.FlushIfDirtyAsync(CancellationToken.None);

        Assert.Equal(500, service.GetTribe(0).Points);
        Assert.Contains(repository.TribePointsDeltaCalls, c => c.TribeId == 0 && c.Delta == 500);
        Assert.Empty(repository.TribeUpdateCalls);
    }

    [Fact]
    public async Task ReconcileAsync_MergesDbPointsTotal_WithLocallyPendingDelta_NeverLosingEither()
    {
        var (service, repository) = CreateInitialized();

        repository.Tribes[0] = repository.Tribes[0] with { Points = 100 };

        service.AddTribePoints(0, 7);

        await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal(107, service.GetTribe(0).Points);
    }

    [Fact]
    public async Task ReconcileAsync_ScalarFieldsUnchangedSinceRead_SwapsInFreshDbValues()
    {
        var (service, repository) = CreateInitialized();
        repository.Row = repository.Row with { Zone038WinTribe = 2, Zone038WinTribeTime = 1234 };

        await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal((byte?)2, service.World.Zone038WinTribe);
        Assert.Equal(1234, service.World.Zone038WinTribeTime);
    }

    [Fact]
    public async Task ReconcileAsync_LocalScalarMutationDuringRead_NeverStompedByTheStaleRead()
    {
        var repository = new RaceOnGetAsyncRepository();
        var service = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        await service.InitializeAsync(CancellationToken.None);
        repository.ServiceUnderTest = service;
        repository.Row = repository.Row with { Zone038WinTribe = 1 };

        await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal((byte?)3, service.World.Zone038WinTribe);
    }

    [Fact]
    public async Task ReconcileAsync_RepositoryFailure_NeverThrows_AndLeavesCacheUntouched()
    {
        var (service, repository) = CreateInitialized();
        service.SetZone038Winner(2);
        repository.ThrowOnGet = true;

        await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal((byte?)2, service.World.Zone038WinTribe);
    }

    [Fact]
    public async Task ReconcileAsync_BeforeInitialize_IsANoOp_NotAThrow()
    {
        var repository = new FakeWorldStateRepository();
        var service = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);

        await service.ReconcileAsync(CancellationToken.None);
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

    private sealed class RaceOnGetAsyncRepository : IWorldStateRepository
    {
        private readonly FakeWorldStateRepository _inner = new();

        public WorldStateService? ServiceUnderTest { get; set; }

        public WorldStateRowDto Row
        {
            get => _inner.Row;
            set => _inner.Row = value;
        }

        public ValueTask EnsureInitializedAsync(CancellationToken ct)
        {
            return _inner.EnsureInitializedAsync(ct);
        }

        public async ValueTask<(WorldStateRowDto? Row, ReadOnlyCollection<WorldStateTribeDto> Tribes,
                ReadOnlyCollection<WorldStateAllianceOfferDto> AllianceOffers)>
            GetAsync(CancellationToken ct)
        {
            ServiceUnderTest?.SetZone038Winner(3);
            return await _inner.GetAsync(ct);
        }

        public ValueTask UpdateAsync(byte? zone038WinTribe, int? zone038WinTribeTime, bool tribeSymbolBattle,
            byte? monsterSymbol, int? monsterSymbolEndTime, byte? highTribe, short updateTribePoint,
            CancellationToken ct)
        {
            return _inner.UpdateAsync(zone038WinTribe, zone038WinTribeTime, tribeSymbolBattle, monsterSymbol,
                monsterSymbolEndTime, highTribe, updateTribePoint, ct);
        }

        public ValueTask UpdateTribeAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, int points,
            bool isClosed, CancellationToken ct)
        {
            return _inner.UpdateTribeAsync(tribeId, symbolDate, hasSymbol, points, isClosed, ct);
        }

        public ValueTask UpdateTribeSymbolStateAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol,
            bool isClosed, CancellationToken ct)
        {
            return _inner.UpdateTribeSymbolStateAsync(tribeId, symbolDate, hasSymbol, isClosed, ct);
        }

        public ValueTask AddTribePointsAsync(byte tribeId, int delta, CancellationToken ct)
        {
            return _inner.AddTribePointsAsync(tribeId, delta, ct);
        }

        public ValueTask SetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted,
            CancellationToken ct)
        {
            return _inner.SetAllianceOfferAsync(fromTribeId, toTribeId, isAccepted, ct);
        }

        public ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct)
        {
            return _inner.GetTribeVotesAsync(tribeId, ct);
        }

        public ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
            short candidateLevel, int killOtherTribeCount, CancellationToken ct)
        {
            return _inner.RegisterTribeVoteCandidateAsync(tribeId, slotIndex, candidateCharacterId, candidateLevel,
                killOtherTribeCount, ct);
        }

        public ValueTask AddTribeVotePointsAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct)
        {
            return _inner.AddTribeVotePointsAsync(tribeId, slotIndex, points, ct);
        }

        public ValueTask ClearTribeVotesAsync(byte tribeId, CancellationToken ct)
        {
            return _inner.ClearTribeVotesAsync(tribeId, ct);
        }
    }

    private sealed class NullRowRepository : IWorldStateRepository
    {
        public ValueTask EnsureInitializedAsync(CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

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
            CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateTribeAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol, int points,
            bool isClosed, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateTribeSymbolStateAsync(byte tribeId, DateTime? symbolDate, bool hasSymbol,
            bool isClosed, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask AddTribePointsAsync(byte tribeId, int delta, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask SetAllianceOfferAsync(byte fromTribeId, byte toTribeId, bool isAccepted,
            CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct)
        {
            return ValueTask.FromResult(new ReadOnlyCollection<TribeVoteDto>([]));
        }

        public ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
            short candidateLevel, int killOtherTribeCount, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask AddTribeVotePointsAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearTribeVotesAsync(byte tribeId, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }
}
