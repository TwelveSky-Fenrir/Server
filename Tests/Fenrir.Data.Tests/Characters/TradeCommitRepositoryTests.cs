using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Characters;

// game.usp_CharacterTradeCommit_ExecuteIdempotent against real SQL Server 2025 -- the C8 anti-dupe variant of
// usp_CharacterTrade_Execute (exercised by CharacterRepositoryTests' sibling coverage of the plain proc
// indirectly via ExecuteTradeAsync elsewhere). The one behavior this proc adds over the plain one -- a retry
// carrying the same TradeToken must never re-apply the additive money legs -- is this file's whole reason to
// exist as a separate suite.
[Collection("SqlServer")]
public class TradeCommitRepositoryTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;
    private readonly ITradeCommitRepository _tradeCommits;

    public TradeCommitRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _tradeCommits = new TradeCommitRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task ExecuteIdempotentAsync_FirstCommit_AppliesMoneyDeltasAndReplacesBothContainers()
    {
        var characterA = await CreateFundedCharacterAsync(5000, 3);
        var characterB = await CreateFundedCharacterAsync(0, 0);
        var token = Guid.NewGuid();

        await _tradeCommits.ExecuteIdempotentAsync(
            token,
            characterA, [new CharacterItemSlotTvp(0, 1026, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)], [],
            -1000, -1,
            characterB, [new CharacterItemSlotTvp(0, 84527, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)], [],
            1000, 1,
            CancellationToken.None);

        var (moneyA, bigMoneyA) = await GetMoneyAsync(characterA);
        Assert.Equal(4000L, moneyA);
        Assert.Equal(2, bigMoneyA);

        var (moneyB, bigMoneyB) = await GetMoneyAsync(characterB);
        Assert.Equal(1000L, moneyB);
        Assert.Equal(1, bigMoneyB);

        Assert.Equal(84527,
            await ScalarAsync<int>(
                $"SELECT ItemId FROM game.CharacterItems WHERE CharacterId = {characterA} AND Container = 0 AND Slot = 0;"));
        Assert.Equal(84527,
            await ScalarAsync<int>(
                $"SELECT ItemId FROM game.CharacterItems WHERE CharacterId = {characterB} AND Container = 0 AND Slot = 0;"));

        Assert.Equal(1, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.TradeCommitLedger WHERE TradeToken = '{token}' AND CharacterA = {characterA} AND CharacterB = {characterB};"));
    }

    // The core anti-dupe guarantee: a retry carrying the SAME token must be a complete no-op, even though the
    // money legs are additive (Money = Money + @Delta) and would otherwise double-apply on a naive replay.
    [Fact]
    public async Task ExecuteIdempotentAsync_RetryWithSameToken_DoesNotReapplyMoney()
    {
        var characterA = await CreateFundedCharacterAsync(5000, 3);
        var characterB = await CreateFundedCharacterAsync(0, 0);
        var token = Guid.NewGuid();

        await _tradeCommits.ExecuteIdempotentAsync(
            token,
            characterA, [], [],
            -1000, -1,
            characterB, [], [],
            1000, 1,
            CancellationToken.None);

        // Retry: same token, same (or any) deltas -- must short-circuit before touching game.Characters at all.
        await _tradeCommits.ExecuteIdempotentAsync(
            token,
            characterA, [], [],
            -1000, -1,
            characterB, [], [],
            1000, 1,
            CancellationToken.None);

        var (moneyA, bigMoneyA) = await GetMoneyAsync(characterA);
        Assert.Equal(4000L, moneyA); // NOT 3000 -- the second call must not have re-debited A.
        Assert.Equal(2, bigMoneyA);

        var (moneyB, bigMoneyB) = await GetMoneyAsync(characterB);
        Assert.Equal(1000L, moneyB); // NOT 2000 -- the second call must not have re-credited B.
        Assert.Equal(1, bigMoneyB);

        // Exactly one ledger row for this token, never two.
        Assert.Equal(1,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.TradeCommitLedger WHERE TradeToken = '{token}';"));
    }

    // A genuinely different trade (a fresh token) between the same two characters must NOT be blocked by the
    // first trade's ledger row -- dedupe is per-token, not a global once-ever-between-these-two-characters gate.
    [Fact]
    public async Task ExecuteIdempotentAsync_DifferentTokenBetweenSameCharacters_CommitsIndependently()
    {
        var characterA = await CreateFundedCharacterAsync(5000, 0);
        var characterB = await CreateFundedCharacterAsync(0, 0);

        await _tradeCommits.ExecuteIdempotentAsync(
            Guid.NewGuid(), characterA, [], [], -1000, 0, characterB, [], [], 1000, 0, CancellationToken.None);
        await _tradeCommits.ExecuteIdempotentAsync(
            Guid.NewGuid(), characterA, [], [], -500, 0, characterB, [], [], 500, 0, CancellationToken.None);

        var (moneyA, _) = await GetMoneyAsync(characterA);
        Assert.Equal(3500L, moneyA);

        var (moneyB, _) = await GetMoneyAsync(characterB);
        Assert.Equal(1500L, moneyB);

        Assert.Equal(2, await ScalarAsync<int>(
            $"SELECT COUNT(*) FROM game.TradeCommitLedger WHERE CharacterA = {characterA} AND CharacterB = {characterB};"));
    }

    [Fact]
    public async Task ExecuteIdempotentAsync_InsufficientBalanceOnCharacterA_ThrowsAndClaimsNoLedgerRow()
    {
        var characterA = await CreateFundedCharacterAsync(100, 0);
        var characterB = await CreateFundedCharacterAsync(0, 0);
        var token = Guid.NewGuid();

        var ex = await Record.ExceptionAsync(() =>
            _tradeCommits.ExecuteIdempotentAsync(
                token, characterA, [], [], -1000, 0, characterB, [], [], 1000, 0,
                CancellationToken.None).AsTask());

        Assert.Equal(50268, Assert.IsType<SqlException>(ex!.InnerException).Number);

        // The failed attempt's ledger INSERT must have rolled back with the rest of the transaction -- a
        // legitimate retry of this exact token (e.g. after the player tops up) must still be possible.
        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.TradeCommitLedger WHERE TradeToken = '{token}';"));

        var (moneyA, _) = await GetMoneyAsync(characterA);
        Assert.Equal(100L, moneyA); // untouched
    }

    [Fact]
    public async Task ExecuteIdempotentAsync_UnknownCharacterB_ThrowsCharacterBCode_AndRollsBackCharacterA()
    {
        var characterA = await CreateFundedCharacterAsync(5000, 0);
        var token = Guid.NewGuid();

        var ex = await Record.ExceptionAsync(() =>
            _tradeCommits.ExecuteIdempotentAsync(
                token, characterA, [], [], -1000, 0, -1, [], [], 1000, 0,
                CancellationToken.None).AsTask());

        Assert.Equal(50269, Assert.IsType<SqlException>(ex!.InnerException).Number);

        // XACT_ABORT unwinds the whole transaction, including Character A's already-applied debit.
        var (moneyA, _) = await GetMoneyAsync(characterA);
        Assert.Equal(5000L, moneyA);

        Assert.Equal(0,
            await ScalarAsync<int>($"SELECT COUNT(*) FROM game.TradeCommitLedger WHERE TradeToken = '{token}';"));
    }

    private async Task<int> CreateFundedCharacterAsync(long money, int bigMoney)
    {
        var accountId = await CreateTestAccountAsync();
        var characterId = await _characters.CreateAsync(
            accountId, 0, NewCharacterName(),
            1, 0, 1, 1,
            1, 0f, 0f, 0f,
            100, 100, 50, 50,
            CancellationToken.None);

        if (money != 0 || bigMoney != 0)
            await _characters.AdjustMoneyAsync(characterId, money, bigMoney, CancellationToken.None);

        return characterId;
    }

    private async Task<int> CreateTestAccountAsync()
    {
        var loginName = $"tradecommit-{Guid.NewGuid():N}";
        return await _accounts.CreateAsync(loginName, RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(16), CancellationToken.None);
    }

    // Name is NVARCHAR(13) and globally unique (UQ_Characters_Name); an 8-char guid slice fits and won't collide.
    private static string NewCharacterName()
    {
        return $"T{Guid.NewGuid():N}"[..8];
    }

    private async Task<(long Money, int BigMoney)> GetMoneyAsync(int characterId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command =
            new SqlCommand($"SELECT Money, BigMoney FROM game.Characters WHERE CharacterId = {characterId};",
                connection);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt64(0), reader.GetInt32(1));
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
