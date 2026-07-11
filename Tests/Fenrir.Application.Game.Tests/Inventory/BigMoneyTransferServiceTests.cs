using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Services.Inventory;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory;

public class BigMoneyTransferServiceTests
{
    private static byte[] EncodeQuantity1(int quantity1)
    {
        var bytes = new byte[28];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), quantity1);
        return bytes;
    }

    [Fact]
    public async Task TransferStoreAsync_Deposit241_DebitsInventoryCreditsStore()
    {
        var repo = new FakeBigMoneyRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new BigMoneyTransferService(repo, eventLog, new TradeRegistry(), ZoneTestKit.CreateRegistry(),
            NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferStoreAsync(241, EncodeQuantity1(5), 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        var call = Assert.NotNull(repo.LastInventoryStore);
        Assert.Equal(10, call.CharacterId);
        Assert.Equal(-5, call.DeltaInventoryBigMoney);
        Assert.Equal(5, call.DeltaStoreBigMoney);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogEmitters.BigMoneyConversionEventCode5, logged.EventCode);
        Assert.Equal(EventLogCategory.BigMoneyConversion, logged.Category);
        Assert.Null(logged.ActorAccountId);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Equal(-5, logged.DeltaMoney);
        Assert.Equal(5, logged.DeltaBigMoney);
    }

    [Fact]
    public async Task TransferStoreAsync_Withdraw244_CreditsInventoryDebitsStore()
    {
        var repo = new FakeBigMoneyRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new BigMoneyTransferService(repo, eventLog, new TradeRegistry(), ZoneTestKit.CreateRegistry(),
            NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferStoreAsync(244, EncodeQuantity1(3), 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        var call = Assert.NotNull(repo.LastInventoryStore);
        Assert.Equal(3, call.DeltaInventoryBigMoney);
        Assert.Equal(-3, call.DeltaStoreBigMoney);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogEmitters.BigMoneyConversionEventCode6, logged.EventCode);
        Assert.Equal(EventLogCategory.BigMoneyConversion, logged.Category);
        Assert.Equal(-3, logged.DeltaMoney);
        Assert.Equal(3, logged.DeltaBigMoney);
    }

    [Fact]
    public async Task TransferStoreAsync_NonPositiveAmount_Aborted_NoRepoCall_NoEventLog()
    {
        var repo = new FakeBigMoneyRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new BigMoneyTransferService(repo, eventLog, new TradeRegistry(), ZoneTestKit.CreateRegistry(),
            NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferStoreAsync(241, EncodeQuantity1(0), 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, outcome.Status);
        Assert.Null(repo.LastInventoryStore);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task TransferStoreAsync_RepositoryThrows_Aborted_NoEventLog()
    {
        var repo = new FakeBigMoneyRepository { ThrowOnAdjust = true };
        var eventLog = new FakeEventLogRepository();
        var service = new BigMoneyTransferService(repo, eventLog, new TradeRegistry(), ZoneTestKit.CreateRegistry(),
            NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferStoreAsync(241, EncodeQuantity1(5), 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, outcome.Status);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task TransferSaveAsync_Deposit242_DebitsInventoryCreditsVault()
    {
        var repo = new FakeBigMoneyRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new BigMoneyTransferService(repo, eventLog, new TradeRegistry(), ZoneTestKit.CreateRegistry(),
            NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferSaveAsync(242, EncodeQuantity1(7), 99, 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        var call = Assert.NotNull(repo.LastInventorySave);
        Assert.Equal(10, call.CharacterId);
        Assert.Equal(-7, call.DeltaInventoryBigMoney);
        Assert.Equal(99, call.AccountId);
        Assert.Equal(7, call.DeltaVaultBigMoney);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogEmitters.BigMoneyConversionEventCode7, logged.EventCode);
        Assert.Equal(EventLogCategory.BigMoneyConversion, logged.Category);
        Assert.Equal(99, logged.ActorAccountId);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Equal(-7, logged.DeltaMoney);
        Assert.Equal(7, logged.DeltaBigMoney);
    }

    [Fact]
    public async Task TransferSaveAsync_Withdraw245_CreditsInventoryDebitsVault()
    {
        var repo = new FakeBigMoneyRepository();
        var eventLog = new FakeEventLogRepository();
        var service = new BigMoneyTransferService(repo, eventLog, new TradeRegistry(), ZoneTestKit.CreateRegistry(),
            NullLogger<BigMoneyTransferService>.Instance);

        var outcome = await service.TransferSaveAsync(245, EncodeQuantity1(2), 99, 10, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        var call = Assert.NotNull(repo.LastInventorySave);
        Assert.Equal(2, call.DeltaInventoryBigMoney);
        Assert.Equal(-2, call.DeltaVaultBigMoney);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal(EventLogEmitters.BigMoneyConversionEventCode8, logged.EventCode);
        Assert.Equal(EventLogCategory.BigMoneyConversion, logged.Category);
        Assert.Equal(99, logged.ActorAccountId);
        Assert.Equal(-2, logged.DeltaMoney);
        Assert.Equal(2, logged.DeltaBigMoney);
    }
}
