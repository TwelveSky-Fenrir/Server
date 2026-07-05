using System.Data;
using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

// Commerce and cash procs against real SQL Server 2025. Each test creates its own account/character(s) so
// tests never depend on execution order.
[Collection("SqlServer")]
public class CommerceProcTests
{
    private readonly IAccountRepository _accounts;
    private readonly ICashRepository _cash;
    private readonly ICharacterRepository _characters;
    private readonly string _connectionString;
    private readonly GiftRepository _gifts;
    private readonly IOfflineShopRepository _offlineShops;

    public CommerceProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _accounts = new AccountRepository(db);
        _characters = new CharacterRepository(db);
        _cash = new CashRepository(db);
        _offlineShops = new OfflineShopRepository(db);
        _gifts = new GiftRepository(db);
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task Cash_DebitAndGrantItem_DebitsAndGrantsAtomically_AndRejectsAnOverdraftWithoutGrantingTheItem()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);
        var itemId = await MinItemIdAsync();

        await ExecProcAsync("game.usp_Cash_Credit",
            ("AccountId", accountId), ("Amount", 100), ("Reason", (byte)1));

        var newBalance = await _cash.DebitAndGrantItemAsync(accountId, 60, 1, itemId,
            characterId, 0, [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)],
            CancellationToken.None);

        Assert.Equal(40, newBalance);
        var items = await GetItemsAsync(characterId, 0);
        Assert.Single(items);

        var ex = await Record.ExceptionAsync(() => _cash.DebitAndGrantItemAsync(accountId, 1000, 1,
            itemId, characterId, 0,
            [
                new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1),
                new CharacterItemSlotTvp(1, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2)
            ],
            CancellationToken.None).AsTask());

        Assert.NotNull(ex);
        Assert.Equal(40, await _cash.GetBalanceAsync(accountId, CancellationToken.None));
        // Failed debit must not have granted the second item either.
        Assert.Single(await GetItemsAsync(characterId, 0));
    }

    [Fact]
    public async Task Character_SpendBloodCoinAndReplaceContainer_SpendsAtomically_AndRejectsInsufficientBalance()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);
        var itemId = await MinItemIdAsync();

        await ExecAsync($"UPDATE game.Characters SET BloodCoin = 10 WHERE CharacterId = {characterId};");

        var newBloodCoin = await _characters.SpendBloodCoinAndReplaceContainerAsync(characterId, -5, 0,
            [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)], CancellationToken.None);

        Assert.Equal(5, newBloodCoin);

        var ex = await Record.ExceptionAsync(() => _characters
            .SpendBloodCoinAndReplaceContainerAsync(characterId, -6, 0, [], CancellationToken.None)
            .AsTask());
        Assert.NotNull(ex);
        Assert.Equal(5,
            await ScalarAsync<int>($"SELECT BloodCoin FROM game.Characters WHERE CharacterId={characterId};"));
    }

    [Fact]
    public async Task Character_ClaimDailyReward_ClaimsOnce_AndRejectsASecondClaimTheSameDay()
    {
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);
        var itemId = await MinItemIdAsync();
        const int today = 20260703;

        await _characters.ClaimDailyRewardAsync(characterId, today, 0,
            [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)], CancellationToken.None);

        var state = await _characters.GetRewardClaimStateAsync(characterId, today, CancellationToken.None);
        Assert.Equal((byte)1, state!.RewardClaimDay);
        Assert.Equal(today, state.RewardClaimDate);

        var ex = await Record.ExceptionAsync(() => _characters
            .ClaimDailyRewardAsync(characterId, today, 0, [], CancellationToken.None).AsTask());
        Assert.NotNull(ex);

        await _characters.ClaimDailyRewardAsync(characterId, today + 1, 0,
            [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)], CancellationToken.None);
        state = await _characters.GetRewardClaimStateAsync(characterId, today + 1, CancellationToken.None);
        Assert.Equal((byte)2, state!.RewardClaimDay);
    }

    [Fact]
    public async Task Character_ClaimDailyReward_FullyClaimedWeek_ResetsOnTheFollowingMonday()
    {
        // Legacy resets RewardClaimDay to 0 every Monday (Server/ts25center/S07_MyGame01.cpp:218-238).
        // 2024-01-01 is a Monday, so day 7 (Sun 01-07) is fully claimed and 01-08 (next Monday) must
        // succeed with a reset instead of being rejected as "fully claimed".
        var accountId = await CreateAccountAsync();
        var characterId = await CreateCharacterAsync(accountId);
        var itemId = await MinItemIdAsync();

        for (var day = 0; day < 7; day++)
            await _characters.ClaimDailyRewardAsync(characterId, 20240101 + day, 0,
                [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)], CancellationToken.None);

        var fullyClaimed = await _characters.GetRewardClaimStateAsync(characterId, 20240107, CancellationToken.None);
        Assert.Equal((byte)7, fullyClaimed!.RewardClaimDay);

        // Merely reading the state on the new week already reports the reset, before any claim runs.
        var readOnNewWeek = await _characters.GetRewardClaimStateAsync(characterId, 20240108, CancellationToken.None);
        Assert.Equal((byte)0, readOnNewWeek!.RewardClaimDay);

        await _characters.ClaimDailyRewardAsync(characterId, 20240108, 0,
            [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)], CancellationToken.None);

        var afterReset = await _characters.GetRewardClaimStateAsync(characterId, 20240108, CancellationToken.None);
        Assert.Equal((byte)1, afterReset!.RewardClaimDay);
        Assert.Equal(20240108, afterReset.RewardClaimDate);
    }

    [Fact]
    public async Task Gift_ClaimIntoVault_PlacesTheItemInTheFirstFreeSlot_AndRejectsAnAlreadyClaimedGift()
    {
        var accountId = await CreateAccountAsync();
        var itemId = await MinItemIdAsync();

        var giftId = await ScalarAsync<int>(
            $"DECLARE @g TABLE(Id INT); INSERT INTO game.Gifts (AccountId, ProductId, Quantity, Value, Status) " +
            $"OUTPUT INSERTED.GiftId INTO @g VALUES ({accountId}, {itemId}, 3, 0, 0); SELECT Id FROM @g;");

        var slot = await _gifts.ClaimIntoVaultAsync(giftId, accountId, CancellationToken.None);
        Assert.Equal(0, slot);

        var vaultItemId = await ScalarAsync<int>(
            $"SELECT ItemId FROM game.AccountVaultItems WHERE AccountId={accountId} AND SlotIndex=0;");
        Assert.Equal(itemId, vaultItemId);

        var ex = await Record.ExceptionAsync(() =>
            _gifts.ClaimIntoVaultAsync(giftId, accountId, CancellationToken.None).AsTask());
        Assert.NotNull(ex);
    }

    [Fact]
    public async Task Gift_ClaimIntoVault_VaultFull_ThrowsAndLeavesTheGiftPending()
    {
        var accountId = await CreateAccountAsync();
        var itemId = await MinItemIdAsync();

        for (var slot = 0; slot < 28; slot++)
            await ExecAsync(
                $"IF NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId={accountId}) INSERT INTO game.AccountVault (AccountId) VALUES ({accountId}); " +
                $"INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber) VALUES ({accountId}, {slot}, {itemId}, 1, 0, 0);");

        var giftId = await ScalarAsync<int>(
            $"DECLARE @g TABLE(Id INT); INSERT INTO game.Gifts (AccountId, ProductId, Quantity, Value, Status) " +
            $"OUTPUT INSERTED.GiftId INTO @g VALUES ({accountId}, {itemId}, 1, 0, 0); SELECT Id FROM @g;");

        var ex = await Record.ExceptionAsync(() =>
            _gifts.ClaimIntoVaultAsync(giftId, accountId, CancellationToken.None).AsTask());
        Assert.NotNull(ex);
        Assert.Equal((byte)0, await ScalarAsync<byte>($"SELECT Status FROM game.Gifts WHERE GiftId={giftId};"));
    }

    [Fact]
    public async Task
        OfflineShop_FullLifecycle_OpenRemovesItemsFromInventory_PurchaseCreditsSeller_WithdrawCreditsCharacter()
    {
        var sellerAccount = await CreateAccountAsync();
        var sellerId = await CreateCharacterAsync(sellerAccount);
        var buyerAccount = await CreateAccountAsync();
        var buyerId = await CreateCharacterAsync(buyerAccount);
        var itemId = await MinItemIdAsync();

        await ExecAsync($"UPDATE game.Characters SET Money = 1000 WHERE CharacterId = {buyerId};");

        await _offlineShops.OpenAndReplaceContainersAsync(sellerId, 37, 20260710,
            "MyShop", 1, 1, 1,
            [new OfflineShopItemSlotTvp(0, itemId, 1, 0, 0, 500, null)],
            [], [], CancellationToken.None);

        var (shop, items) = await _offlineShops.GetByCharacterAsync(sellerId, CancellationToken.None);
        Assert.Equal((byte)1, shop!.ShopState);
        Assert.Single(items);
        Assert.Empty(await GetItemsAsync(sellerId, 0));

        // Re-opening while the shop still holds unclaimed value is refused.
        var reopenEx = await Record.ExceptionAsync(() => _offlineShops.OpenAndReplaceContainersAsync(sellerId, 37,
            20260710, "MyShop", 1, 1, 1, [new OfflineShopItemSlotTvp(0, itemId, 1, 0, 0, 500, null)], [], [],
            CancellationToken.None).AsTask());
        Assert.NotNull(reopenEx);

        await _offlineShops.ExecutePurchaseAsync(sellerId, 0, itemId, 1,
            0, 500, buyerId, 0,
            [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)], CancellationToken.None);

        Assert.Single(await GetItemsAsync(buyerId, 0));
        var (shopAfterSale, itemsAfterSale) = await _offlineShops.GetByCharacterAsync(sellerId, CancellationToken.None);
        Assert.Equal(500, shopAfterSale!.Money);
        Assert.Empty(itemsAfterSale);

        // Buying the same now-gone slot again fails cleanly (CAS).
        var staleEx = await Record.ExceptionAsync(() => _offlineShops.ExecutePurchaseAsync(sellerId, 0, itemId, 1, 0,
            500, buyerId, 0, [], CancellationToken.None).AsTask());
        Assert.NotNull(staleEx);

        await _offlineShops.SetStateAsync(sellerId, 0, CancellationToken.None);
        await _offlineShops.WithdrawMoneyAsync(sellerId, 500, 0, CancellationToken.None);

        Assert.Equal(500L, await ScalarAsync<long>($"SELECT Money FROM game.Characters WHERE CharacterId={sellerId};"));
        var (shopAfterWithdraw, _) = await _offlineShops.GetByCharacterAsync(sellerId, CancellationToken.None);
        Assert.Equal(0, shopAfterWithdraw!.Money);
    }

    [Fact]
    public async Task OfflineShop_RetrieveItem_RequiresTheShopClosed_AndMovesTheItemBackToTheOwnersInventory()
    {
        var sellerAccount = await CreateAccountAsync();
        var sellerId = await CreateCharacterAsync(sellerAccount);
        var itemId = await MinItemIdAsync();

        await _offlineShops.OpenAndReplaceContainersAsync(sellerId, 37, 20260710, "MyShop", 1, 1, 1,
            [new OfflineShopItemSlotTvp(0, itemId, 2, 0, 0, 500, null)], [], [], CancellationToken.None);

        var openEx = await Record.ExceptionAsync(() => _offlineShops.RetrieveItemAndReplaceContainerAsync(sellerId,
            0, itemId, 2, 0, 1, [new CharacterItemSlotTvp(0, itemId, 2, 0, 0, 0, 0, 0, 0, 0, 0, 1)],
            CancellationToken.None).AsTask());
        Assert.NotNull(openEx);

        await _offlineShops.SetStateAsync(sellerId, 0, CancellationToken.None);

        await _offlineShops.RetrieveItemAndReplaceContainerAsync(sellerId, 0, itemId, 2, 0, 1,
            [new CharacterItemSlotTvp(0, itemId, 2, 0, 0, 0, 0, 0, 0, 0, 0, 1)], CancellationToken.None);

        Assert.Single(await GetItemsAsync(sellerId, 1));
        var (_, itemsAfter) = await _offlineShops.GetByCharacterAsync(sellerId, CancellationToken.None);
        Assert.Empty(itemsAfter);
    }

    [Fact]
    public async Task PshopPurchase_Execute_MovesMoneyAndItemsBetweenBothCharacters_AndRejectsInsufficientBuyerFunds()
    {
        var sellerAccount = await CreateAccountAsync();
        var sellerId = await CreateCharacterAsync(sellerAccount);
        var buyerAccount = await CreateAccountAsync();
        var buyerId = await CreateCharacterAsync(buyerAccount);
        var itemId = await MinItemIdAsync();

        await ExecAsync($"UPDATE game.Characters SET Money = 1000 WHERE CharacterId = {buyerId};");

        await _characters.ExecutePshopPurchaseAsync(sellerId, 0, [],
            buyerId, 0, [new CharacterItemSlotTvp(0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)],
            400, CancellationToken.None);

        Assert.Equal(600L, await ScalarAsync<long>($"SELECT Money FROM game.Characters WHERE CharacterId={buyerId};"));
        Assert.Equal(400L, await ScalarAsync<long>($"SELECT Money FROM game.Characters WHERE CharacterId={sellerId};"));
        Assert.Single(await GetItemsAsync(buyerId, 0));

        var ex = await Record.ExceptionAsync(() => _characters.ExecutePshopPurchaseAsync(sellerId, 0, [], buyerId, 0,
            [], 1000, CancellationToken.None).AsTask());
        Assert.NotNull(ex);
        Assert.Equal(600L, await ScalarAsync<long>($"SELECT Money FROM game.Characters WHERE CharacterId={buyerId};"));
    }

    private async Task<int> CreateAccountAsync()
    {
        return await _accounts.CreateAsync($"commercetest-{Guid.NewGuid():N}", RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(16), CancellationToken.None);
    }

    private Task<int> CreateCharacterAsync(int accountId)
    {
        var name = $"T{Guid.NewGuid():N}"[..8];
        return _characters.CreateAsync(accountId, 0, name, 1, 0, 1, 1, 1, 0f, 0f, 0f, 100, 100, 50, 50,
            CancellationToken.None).AsTask();
    }

    private Task<int> MinItemIdAsync()
    {
        return ScalarAsync<int>("SELECT MIN(ItemId) FROM world.Items;");
    }

    private async Task<List<int>> GetItemsAsync(int characterId, byte container)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            $"SELECT ItemId FROM game.CharacterItems WHERE CharacterId={characterId} AND Container={container};",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<int>();
        while (await reader.ReadAsync())
            result.Add(reader.GetInt32(0));
        return result;
    }

    private async Task ExecProcAsync(string procName, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(procName, connection) { CommandType = CommandType.StoredProcedure };
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
