using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

/// <summary>
///     game.usp_WorldState_{EnsureInitialized,Get,Update}/usp_WorldStateTribe_Update/
///     usp_WorldStateAllianceOffer_Set/usp_TribeVote_GetByTribe against real SQL Server 2025, through
///     <see cref="WorldStateRepository" /> exactly as <c>WorldStateService</c> calls it.
/// </summary>
/// <remarks>
///     game.WorldState/WorldStateTribes are true singletons (1 + 4 fixed rows) shared by every test in the
///     "SqlServer" collection for the whole assembly run, in no guaranteed order -- every assertion below
///     reads back only what THIS test itself just wrote, never an assumed pristine/default value, so it stays
///     correct regardless of what any other test in the suite already did to the same rows.
/// </remarks>
[Collection("SqlServer")]
public class WorldStateRepositoryProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly IWorldStateRepository _repository;

    public WorldStateRepositoryProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _repository = new WorldStateRepository(db);
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
    }

    /// <summary>
    ///     game.TribeVotes.CandidateCharacterId is a real FK into game.Characters -- every Register test needs
    ///     an actual row to point at, not a throwaway int.
    /// </summary>
    private async Task<int> CreateCharacterAsync()
    {
        var accountId = await _accounts.CreateAsync($"tribevotetest-{Guid.NewGuid():N}",
            RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(16), CancellationToken.None);

        return await _characters.CreateAsync(
            accountId, 0, $"V{Guid.NewGuid():N}"[..8],
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);
    }

    [Fact]
    public async Task EnsureInitializedAsync_SecondCall_NeverResetsAlreadyPersistedState()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);
        await _repository.UpdateAsync(1, 101, true,
            null, null, null, 0,
            CancellationToken.None);
        await _repository.UpdateTribeAsync(0, null, false, 999, true,
            CancellationToken.None);

        await _repository.EnsureInitializedAsync(CancellationToken.None); // must stay a no-op from here on

        var (row, tribes, _) = await _repository.GetAsync(CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal((byte?)1, row.Zone038WinTribe);
        Assert.Equal(101, row.Zone038WinTribeTime);
        Assert.Equal(4, tribes.Count); // fixed domain: still exactly 4 tribe rows, never duplicated
        Assert.Contains(tribes, t => t is { TribeId: 0, Points: 999, IsClosed: true, HasSymbol: false });
    }

    /// <summary>
    ///     Reproduces the sharded-boot race that used to throw a PK_WorldState violation: every
    ///     <c>game-shard-NN</c> process calls <c>EnsureInitializedAsync</c> once at startup with no external
    ///     serialization between them, so several callers can hit the procedure at the same instant. The
    ///     sp_getapplock guard added by 039_worldstate_ensureinitialized_concurrency_fix.sql must serialize
    ///     them instead of letting two concurrent "NOT EXISTS" checks both pass.
    /// </summary>
    [Fact]
    public async Task EnsureInitializedAsync_CalledConcurrently_NeverThrowsAndSeedsExactlyOnce()
    {
        var callers = Enumerable.Range(0, 8)
            .Select(_ => _repository.EnsureInitializedAsync(CancellationToken.None).AsTask());

        await Task.WhenAll(callers); // must not throw PK_WorldState/PK_Tribes/etc.

        var (row, tribes, _) = await _repository.GetAsync(CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal(4, tribes.Count);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, tribes.Select(t => t.TribeId).OrderBy(id => id));
    }

    [Fact]
    public async Task UpdateAsync_ReplacesEveryScalarField_AndIsReadBackByGetAsync()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);

        await _repository.UpdateAsync(2, 1430, true,
            1, 900, 3, 2, CancellationToken.None);

        var (row, _, _) = await _repository.GetAsync(CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal((byte?)2, row.Zone038WinTribe);
        Assert.Equal(1430, row.Zone038WinTribeTime);
        Assert.True(row.TribeSymbolBattle);
        Assert.Equal((byte?)1, row.MonsterSymbol);
        Assert.Equal(900, row.MonsterSymbolEndTime);
        Assert.Equal((byte?)3, row.HighTribe);
        Assert.Equal((short)2, row.UpdateTribePoint);
    }

    [Fact]
    public async Task UpdateTribeAsync_ReplacesOnlyTheTargetedTribeRow()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);
        var (_, before, _) = await _repository.GetAsync(CancellationToken.None);
        var othersBefore = before.Where(t => t.TribeId != 2).ToDictionary(t => t.TribeId);
        var symbolDate = new DateTime(2026, 1, 15, 3, 4, 5, DateTimeKind.Utc);

        await _repository.UpdateTribeAsync(2, symbolDate, false, 77, true,
            CancellationToken.None);

        var (_, after, _) = await _repository.GetAsync(CancellationToken.None);

        var updated = Assert.Single(after, t => t.TribeId == 2);
        Assert.Equal(symbolDate, updated.SymbolDate);
        Assert.False(updated.HasSymbol);
        Assert.Equal(77, updated.Points);
        Assert.True(updated.IsClosed);

        // Every other tribe's row is byte-for-byte whatever it already was -- untouched by this call.
        foreach (var tribe in after.Where(t => t.TribeId != 2))
            Assert.Equal(othersBefore[tribe.TribeId], tribe);
    }

    [Fact]
    public async Task UpdateTribeSymbolStateAsync_ReplacesSymbolFields_WithoutTouchingPoints()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);
        var (_, before, _) = await _repository.GetAsync(CancellationToken.None);
        var pointsBefore = before.Single(t => t.TribeId == 3).Points;
        var symbolDate = new DateTime(2026, 2, 1, 6, 7, 8, DateTimeKind.Utc);

        await _repository.UpdateTribeSymbolStateAsync(3, symbolDate, false, true,
            CancellationToken.None);

        var (_, after, _) = await _repository.GetAsync(CancellationToken.None);
        var updated = Assert.Single(after, t => t.TribeId == 3);

        Assert.Equal(symbolDate, updated.SymbolDate);
        Assert.False(updated.HasSymbol);
        Assert.True(updated.IsClosed);
        // Points is deliberately excluded from this proc -- untouched by this call, unlike UpdateTribeAsync.
        Assert.Equal(pointsBefore, updated.Points);
    }

    [Fact]
    public async Task AddTribePointsAsync_AddsToExistingTotal_WithoutTouchingSymbolStateFields()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);
        var symbolDate = new DateTime(2026, 3, 1, 1, 1, 1, DateTimeKind.Utc);
        await _repository.UpdateTribeSymbolStateAsync(1, symbolDate, true, false,
            CancellationToken.None);
        var (_, before, _) = await _repository.GetAsync(CancellationToken.None);
        var pointsBefore = before.Single(t => t.TribeId == 1).Points;

        await _repository.AddTribePointsAsync(1, 15, CancellationToken.None);
        await _repository.AddTribePointsAsync(1, -3, CancellationToken.None);

        var (_, after, _) = await _repository.GetAsync(CancellationToken.None);
        var updated = Assert.Single(after, t => t.TribeId == 1);

        Assert.Equal(pointsBefore + 15 - 3, updated.Points);
        // Symbol-state fields untouched by this proc, unlike UpdateTribeAsync's whole-row overwrite.
        Assert.Equal(symbolDate, updated.SymbolDate);
        Assert.True(updated.HasSymbol);
        Assert.False(updated.IsClosed);
    }

    [Fact]
    public async Task SetAllianceOfferAsync_UpsertsTheSamePair_InsteadOfDuplicating()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);

        await _repository.SetAllianceOfferAsync(0, 1, false, CancellationToken.None);
        await _repository.SetAllianceOfferAsync(0, 1, true, CancellationToken.None);

        var (_, _, allianceOffers) = await _repository.GetAsync(CancellationToken.None);
        var matching = allianceOffers.Where(o => o is { FromTribeId: 0, ToTribeId: 1 }).ToList();

        var offer = Assert.Single(matching);
        Assert.True(offer.IsAccepted);
    }

    [Fact]
    public async Task GetTribeVotesAsync_NoRegisteredCandidates_ReturnsEmpty()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);

        // Tribe 3's candidate slots are never touched by any other test in this suite, so this stays
        // reliably empty regardless of run order.
        var votes = await _repository.GetTribeVotesAsync(3, CancellationToken.None);

        Assert.Empty(votes);
    }

    [Fact]
    public async Task RegisterTribeVoteCandidateAsync_ThenGetTribeVotesAsync_ReadsItBackOrderedByVotePointDesc()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);
        var candidate1 = await CreateCharacterAsync();
        var candidate2 = await CreateCharacterAsync();

        await _repository.RegisterTribeVoteCandidateAsync(1, 0, candidate1, 150,
            1500, CancellationToken.None);
        await _repository.RegisterTribeVoteCandidateAsync(1, 1, candidate2, 160,
            2500, CancellationToken.None);

        var votes = await _repository.GetTribeVotesAsync(1, CancellationToken.None);

        Assert.Equal(2, votes.Count);
        Assert.Contains(votes,
            v => v.SlotIndex == 0 && v.CandidateCharacterId == candidate1 && v.KillOtherTribeCount == 1500);
        Assert.Contains(votes,
            v => v.SlotIndex == 1 && v.CandidateCharacterId == candidate2 && v.KillOtherTribeCount == 2500);
    }

    [Fact]
    public async Task RegisterTribeVoteCandidateAsync_CalledTwiceForTheSameSlot_ReplacesTheOccupant_NotDuplicatesIt()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);
        var firstCandidate = await CreateCharacterAsync();
        var secondCandidate = await CreateCharacterAsync();

        await _repository.RegisterTribeVoteCandidateAsync(2, 4, firstCandidate, 150,
            1200, CancellationToken.None);
        await _repository.RegisterTribeVoteCandidateAsync(2, 4, secondCandidate, 155,
            3000, CancellationToken.None);

        var votes = await _repository.GetTribeVotesAsync(2, CancellationToken.None);

        var slot4 = Assert.Single(votes, v => v.SlotIndex == 4);
        Assert.Equal(secondCandidate, slot4.CandidateCharacterId);
        Assert.Equal(3000, slot4.KillOtherTribeCount);
        Assert.Equal(0, slot4.VotePoint); // displacing a candidate resets the tally for that slot
    }

    [Fact]
    public async Task AddTribeVotePointsAsync_AccumulatesAcrossMultipleVoters()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);
        var candidate = await CreateCharacterAsync();
        await _repository.RegisterTribeVoteCandidateAsync(0, 2, candidate, 150,
            1200, CancellationToken.None);

        await _repository.AddTribeVotePointsAsync(0, 2, 44, CancellationToken.None);
        await _repository.AddTribeVotePointsAsync(0, 2, 51, CancellationToken.None);

        var votes = await _repository.GetTribeVotesAsync(0, CancellationToken.None);
        var slot2 = Assert.Single(votes, v => v.SlotIndex == 2);
        Assert.Equal(95, slot2.VotePoint);
    }

    [Fact]
    public async Task ClearTribeVotesAsync_RemovesEveryCandidateForThatTribeOnly()
    {
        await _repository.EnsureInitializedAsync(CancellationToken.None);
        var tribe3Candidate = await CreateCharacterAsync();
        var tribe1Candidate = await CreateCharacterAsync();
        await _repository.RegisterTribeVoteCandidateAsync(3, 0, tribe3Candidate, 150,
            1200, CancellationToken.None);
        await _repository.RegisterTribeVoteCandidateAsync(1, 9, tribe1Candidate, 150,
            1200, CancellationToken.None);

        await _repository.ClearTribeVotesAsync(3, CancellationToken.None);

        Assert.Empty(await _repository.GetTribeVotesAsync(3, CancellationToken.None));
        Assert.NotEmpty(await _repository.GetTribeVotesAsync(1, CancellationToken.None));
    }
}
