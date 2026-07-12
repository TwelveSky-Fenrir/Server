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

        await _bigMoney.AdjustBigMoneyStoreAsync(characterId, -2, 2, CancellationToken.None);

        var afterDeposit = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterDeposit);
        Assert.Equal(1, afterDeposit.Character.BigMoney);
        Assert.Equal(2, afterDeposit.Character.BigStoreMoney);

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

        await _bigMoney.AdjustBigMoneyBankAsync(characterId, -3, accountId, 3, CancellationToken.None);

        var afterDeposit = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterDeposit);
        Assert.Equal(2, afterDeposit.Character.BigMoney);

        var (balanceAfterDeposit, _) = await _accountVault.GetAsync(accountId, CancellationToken.None);
        Assert.NotNull(balanceAfterDeposit);
        Assert.Equal(3L, balanceAfterDeposit.Money2);

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
        // usp_AccountVault_TransferBigMoneyWithCharacter's character-side guard throws 50353, not 50350 --
        // 50350/50351 were reassigned to the pet-bag procedures during the Wave-7/Wave-8 BigMoney
        // implementation collision (see Migrations/Seed/admin/018_error_catalog_bigmoney_store_and_vault.sql);
        // this assertion was stale until the economy-hardening pass (2026-07-12) corrected it.
        if (sqlException is not null)
            Assert.Equal(50353, sqlException.Number);

        var afterRejected = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterRejected);
        Assert.Equal(1, afterRejected.Character.BigMoney);

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
        // Same 50350->50353 renumbering as above; this is the vault-side guard, which throws 50354 not 50351.
        if (sqlException is not null)
            Assert.Equal(50354, sqlException.Number);

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

        await _bigMoney.AdjustBigMoneyConversionAsync(characterId, -1_000_000_000L, 1, CancellationToken.None);

        var afterConvertUp = await _characters.GetWorldEntryBundleAsync(characterId, CancellationToken.None);
        Assert.NotNull(afterConvertUp);
        Assert.Equal(0L, afterConvertUp.Character.Money);
        Assert.Equal(1, afterConvertUp.Character.BigMoney);

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
