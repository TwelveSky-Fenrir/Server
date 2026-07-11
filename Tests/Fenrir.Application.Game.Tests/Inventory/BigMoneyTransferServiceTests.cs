using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Services.Inventory;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Coverage for <see cref="BigMoneyTransferService" /> -- the CZ_PROCESS_DATA_SEND BigMoney ("1B"/"1 Md")
///     Store (tSort 241/244) and Save/vault (tSort 242/245) transfer families. See
///     <c>BigMoneyTransferPolicyTests</c>/<c>BigMoneyUnitConversionPolicyTests</c> for the pure-policy-level
///     coverage of the underlying rule set (not directly exercised by this service -- see
///     <see cref="BigMoneyTransferService" />'s own remarks for why the balance/cap checks are enforced purely
///     DB-side here, matching <c>GenericActionService.TransferStoreMoneyAsync</c>'s own established
///     precedent).
/// </summary>
public class BigMoneyTransferServiceTests
{
    /// <summary>Encodes a raw DefaultPData payload (7 sequential little-endian int32 fields, wire order) -- only Quantity1 varies here.</summary>
    private static byte[] EncodeQuantity1(int quantity1)
    {
        var bytes = new byte[28];
        // Page1, Index1
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), quantity1); // Quantity1 is the 3rd int32 field.
        return bytes;
    }

    [Fact]
    public async Task TransferStoreAsync_Deposit241_DebitsInventoryCreditsStore()
    {
        var repo = new FakeBigMoneyRepository();
        var service = new BigMoneyTransferService(repo, NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferStoreAsync(241, EncodeQuantity1(5), 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        var call = Assert.NotNull(repo.LastInventoryStore);
        Assert.Equal(10, call.CharacterId);
        Assert.Equal(-5, call.DeltaInventoryBigMoney);
        Assert.Equal(5, call.DeltaStoreBigMoney);
    }

    [Fact]
    public async Task TransferStoreAsync_Withdraw244_CreditsInventoryDebitsStore()
    {
        var repo = new FakeBigMoneyRepository();
        var service = new BigMoneyTransferService(repo, NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferStoreAsync(244, EncodeQuantity1(3), 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        var call = Assert.NotNull(repo.LastInventoryStore);
        Assert.Equal(3, call.DeltaInventoryBigMoney);
        Assert.Equal(-3, call.DeltaStoreBigMoney);
    }

    [Fact]
    public async Task TransferStoreAsync_NonPositiveAmount_Aborted_NoRepoCall()
    {
        var repo = new FakeBigMoneyRepository();
        var service = new BigMoneyTransferService(repo, NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferStoreAsync(241, EncodeQuantity1(0), 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, outcome.Status);
        Assert.Null(repo.LastInventoryStore);
    }

    [Fact]
    public async Task TransferStoreAsync_RepositoryThrows_Aborted()
    {
        var repo = new FakeBigMoneyRepository { ThrowOnAdjust = true };
        var service = new BigMoneyTransferService(repo, NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferStoreAsync(241, EncodeQuantity1(5), 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, outcome.Status);
    }

    [Fact]
    public async Task TransferSaveAsync_Deposit242_DebitsInventoryCreditsVault()
    {
        var repo = new FakeBigMoneyRepository();
        var service = new BigMoneyTransferService(repo, NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferSaveAsync(242, EncodeQuantity1(7), 99, 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        var call = Assert.NotNull(repo.LastInventorySave);
        Assert.Equal(10, call.CharacterId);
        Assert.Equal(-7, call.DeltaInventoryBigMoney);
        Assert.Equal(99, call.AccountId);
        Assert.Equal(7, call.DeltaVaultBigMoney);
    }

    [Fact]
    public async Task TransferSaveAsync_Withdraw245_CreditsInventoryDebitsVault()
    {
        var repo = new FakeBigMoneyRepository();
        var service = new BigMoneyTransferService(repo, NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferSaveAsync(245, EncodeQuantity1(2), 99, 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        var call = Assert.NotNull(repo.LastInventorySave);
        Assert.Equal(2, call.DeltaInventoryBigMoney);
        Assert.Equal(-2, call.DeltaVaultBigMoney);
    }
}
