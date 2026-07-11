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

// game.usp_Character_AdjustBigMoneyStore / usp_AccountVault_TransferBigMoneyWithCharacter /
// usp_Character_AdjustBigMoneyConversion against real SQL Server 2025 -- the three BigMoney ("1B")
// transfer/conversion primitives backing CZ_PROCESS_DATA_SEND tSort 241/242/244/245/246/247 (the
// C8-bank-store contract's big-money family). Each test creates its own account/character so tests never
// depend on execution order.
[Collection("SqlServer")]
public class BigMoneyRepositoryTests
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountVaultRepository _accountVault;
    private readonly IBigMoneyRepository _bigMoney;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;

    public BigMoneyRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _accountVault = new AccountVaultRepository(db);
        _bigMoney = new BigMoneyRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task AdjustBigMoneyStoreAsync_TransfersBetweenInventoryAndStoreBigMoney()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);

        await ExecAsync($"UPDATE game.Characters SET BigMoney = 3 WHERE CharacterId = {characterId};");

        // tSort 241 -- inventory -> store: move 2 units.
        await _bigMoney.AdjustBigMoneyStoreAsync(characterId, -2, 2, CancellationToken.None);

        var afterDeposit = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterDeposit);
        Assert.Equal(1, afterDeposit.Character.BigMoney);
        Assert.Equal(2, afterDeposit.Character.BigStoreMoney);

        // tSort 244 -- store -> inventory: move the 2 units back.
        await _bigMoney.AdjustBigMoneyStoreAsync(characterId, 2, -2, CancellationToken.None);

        var afterWithdraw = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterWithdraw);
        Assert.Equal(3, afterWithdraw.Character.BigMoney);
        Assert.Equal(0, afterWithdraw.Character.BigStoreMoney);
    }

    [Fact]
    public async Task AdjustBigMoneyStoreAsync_RejectsAnAdjustmentThatWouldExceedThe999UnitCap()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);

        await ExecAsync(
            $"UPDATE game.Characters SET BigMoney = 1, BigStoreMoney = 999 WHERE CharacterId = {characterId};");

        // Moving 1 more unit into BigStoreMoney would land it at 1000, over MAX_NUMBER_SIZE2 (999).
        var ex = await Record.ExceptionAsync(() =>
            _bigMoney.AdjustBigMoneyStoreAsync(characterId, -1, 1, CancellationToken.None).AsTask());

        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50349, sqlException.Number);

        var afterRejected = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterRejected);
        Assert.Equal(1, afterRejected.Character.BigMoney);
        Assert.Equal(999, afterRejected.Character.BigStoreMoney);
    }

    [Fact]
    public async Task AdjustBigMoneyBankAsync_AutoCreatesTheVaultRow_AndTransfersBetweenInventoryAndVault()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);

        await ExecAsync($"UPDATE game.Characters SET BigMoney = 5 WHERE CharacterId = {characterId};");

        // No game.AccountVault row exists for this account yet -- tSort 242 (inventory -> bank) must
        // auto-create it, mirroring usp_AccountVault_TransferMoneyWithCharacter's own posture.
        await _bigMoney.AdjustBigMoneyBankAsync(characterId, -3, accountId, 3, CancellationToken.None);

        var afterDeposit = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterDeposit);
        Assert.Equal(2, afterDeposit.Character.BigMoney);

        var (balanceAfterDeposit, _) = await _accountVault.GetAsync(accountId, CancellationToken.None);
        Assert.NotNull(balanceAfterDeposit);
        Assert.Equal(3L, balanceAfterDeposit.Money2);

        // tSort 245 -- bank -> inventory: move the 3 units back.
        await _bigMoney.AdjustBigMoneyBankAsync(characterId, 3, accountId, -3, CancellationToken.None);

        var afterWithdraw = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterWithdraw);
        Assert.Equal(5, afterWithdraw.Character.BigMoney);

        var (balanceAfterWithdraw, _) = await _accountVault.GetAsync(accountId, CancellationToken.None);
        Assert.NotNull(balanceAfterWithdraw);
        Assert.Equal(0L, balanceAfterWithdraw.Money2);
    }

    [Fact]
    public async Task AdjustBigMoneyBankAsync_RejectsInsufficientInventoryBigMoney_WithoutTouchingTheVault()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);

        await ExecAsync($"UPDATE game.Characters SET BigMoney = 1 WHERE CharacterId = {characterId};");

        var ex = await Record.ExceptionAsync(() =>
            _bigMoney.AdjustBigMoneyBankAsync(characterId, -2, accountId, 2, CancellationToken.None).AsTask());

        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50350, sqlException.Number);

        var afterRejected = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterRejected);
        Assert.Equal(1, afterRejected.Character.BigMoney);

        // The Characters-side guard fails first (inside the same transaction as the auto-create), so the
        // vault row must never observe a credit -- verify it stays at its untouched default of 0.
        var (balance, _) = await _accountVault.GetAsync(accountId, CancellationToken.None);
        if (balance is not null)
            Assert.Equal(0L, balance.Money2);
    }

    [Fact]
    public async Task AdjustBigMoneyBankAsync_RejectsAnAdjustmentThatWouldExceedTheVaults999UnitCap()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);

        await ExecAsync($"UPDATE game.Characters SET BigMoney = 5 WHERE CharacterId = {characterId};");
        await ExecAsync(
            $"IF NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId={accountId}) INSERT INTO game.AccountVault (AccountId) VALUES ({accountId});");
        await ExecAsync($"UPDATE game.AccountVault SET Money2 = 999 WHERE AccountId = {accountId};");

        var ex = await Record.ExceptionAsync(() =>
            _bigMoney.AdjustBigMoneyBankAsync(characterId, -1, accountId, 1, CancellationToken.None).AsTask());

        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50351, sqlException.Number);

        // XACT_ABORT + THROW rolls back the whole transaction, including the Characters-side debit that
        // already committed inside this same batch -- verify it was rolled back, not left half-applied.
        var afterRejected = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterRejected);
        Assert.Equal(5, afterRejected.Character.BigMoney);
    }

    [Fact]
    public async Task AdjustBigMoneyConversionAsync_AppliesCappedMoneyAndBigMoneyDeltasTogether()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);

        await ExecAsync($"UPDATE game.Characters SET Money = 1000000000 WHERE CharacterId = {characterId};");

        // tSort 246 shape (BigMoneyUnitConversionPolicy.ResolveMoneyToBigMoney): full requested amount
        // debited from Money, exactly +1 credited to BigMoney.
        await _bigMoney.AdjustBigMoneyConversionAsync(characterId, -1_000_000_000L, 1, CancellationToken.None);

        var afterConvertUp = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterConvertUp);
        Assert.Equal(0L, afterConvertUp.Character.Money);
        Assert.Equal(1, afterConvertUp.Character.BigMoney);

        // tSort 247 shape (ResolveBigMoneyToMoney): exactly -1 BigMoney, exactly +1,000,000,000 Money.
        await _bigMoney.AdjustBigMoneyConversionAsync(characterId, 1_000_000_000L, -1, CancellationToken.None);

        var afterConvertDown = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterConvertDown);
        Assert.Equal(1_000_000_000L, afterConvertDown.Character.Money);
        Assert.Equal(0, afterConvertDown.Character.BigMoney);
    }

    [Fact]
    public async Task AdjustBigMoneyConversionAsync_RejectsAnAdjustmentThatWouldExceedEitherCap()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);

        await ExecAsync(
            $"UPDATE game.Characters SET Money = 1000000000, BigMoney = 999 WHERE CharacterId = {characterId};");

        // Converting up again would push BigMoney to 1000, over MAX_NUMBER_SIZE2 (999).
        var ex = await Record.ExceptionAsync(() =>
            _bigMoney.AdjustBigMoneyConversionAsync(characterId, -1_000_000_000L, 1, CancellationToken.None)
                .AsTask());

        Assert.NotNull(ex);
        var sqlException = ex as SqlException ?? ex!.InnerException as SqlException;
        if (sqlException is not null)
            Assert.Equal(50352, sqlException.Number);

        var afterRejected = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterRejected);
        Assert.Equal(1_000_000_000L, afterRejected.Character.Money);
        Assert.Equal(999, afterRejected.Character.BigMoney);
    }

    private async Task<int> CreateAccountAsync()
    {
        return await _accounts.CreateAsync($"bigmoneytest-{Guid.NewGuid():N}", RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(16), CancellationToken.None);
    }

    private Task<int> CreateCharacterAsync(int accountId)
    {
        var name = $"T{Guid.NewGuid():N}"[..8];
        return _characters.CreateAsync(accountId, 0, name, 1, 0, 1, 1, 1, 0f, 0f, 0f, 100, 100, 50, 50,
            CancellationToken.None).AsTask();
    }

    private async Task ExecAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
