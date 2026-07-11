using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Application.Game.Tests.Audit;

public class EventLogEmittersTests
{
    [Fact]
    public async Task LogCashShopPurchaseAsync_WritesSingleRecord_WithNoDeltaMoney()
    {
        var fake = new FakeEventLogRepository();

        await fake.LogCashShopPurchaseAsync(10, 20, 5001, 3, 0,
            CancellationToken.None);

        var e = Assert.Single(fake.LoggedEvents);
        Assert.Equal(EventLogEmitters.CashShopPurchaseEventCode, e.EventCode);
        Assert.Equal(EventLogCategory.CashShopPurchase, e.Category);
        Assert.Equal(10, e.ActorAccountId);
        Assert.Equal(20, e.ActorCharacterId);
        Assert.Equal(5001, e.ItemId);
        Assert.Equal(3, e.Quantity);
        Assert.Null(e.DeltaMoney);
        Assert.Null(e.Payload);
    }

    [Fact]
    public async Task LogCashShopPurchaseAsync_NonZeroSerial_IncludesSerialInPayload()
    {
        var fake = new FakeEventLogRepository();

        await fake.LogCashShopPurchaseAsync(1, 2, 3, 4, 999, CancellationToken.None);

        Assert.Equal("Serial=999", Assert.Single(fake.LoggedEvents).Payload);
    }

    [Fact]
    public async Task LogGroundItemGainAsync_NonEliteItem_WritesOnlyTheGainRecord()
    {
        var fake = new FakeEventLogRepository();

        await fake.LogGroundItemGainAsync(1, 2, 100, 1,
            1, CancellationToken.None);

        var e = Assert.Single(fake.LoggedEvents);
        Assert.Equal(EventLogEmitters.GroundItemGainEventCode, e.EventCode);
        Assert.Equal(EventLogCategory.ItemPickup, e.Category);
        Assert.Equal(100, e.ItemId);
        Assert.Equal(1, e.Quantity);
    }

    [Fact]
    public async Task LogGroundItemGainAsync_EliteItem_WritesBothRecordsInOrder()
    {
        var fake = new FakeEventLogRepository();

        await fake.LogGroundItemGainAsync(1, 2, 100, 1, EventLogEmitters.GroundItemEliteTypeThreshold,
            CancellationToken.None);

        Assert.Equal(2, fake.LoggedEvents.Count);
        Assert.Equal(EventLogEmitters.GroundItemGainEventCode, fake.LoggedEvents[0].EventCode);
        Assert.Equal(EventLogEmitters.GroundItemGainEliteEventCode, fake.LoggedEvents[1].EventCode);
        Assert.All(fake.LoggedEvents, e => Assert.Equal(EventLogCategory.ItemPickup, e.Category));
    }

    [Fact]
    public async Task LogGroundItemGainAsync_ItemTypeBelowThreshold_NoEliteRecord()
    {
        var fake = new FakeEventLogRepository();

        await fake.LogGroundItemGainAsync(1, 2, 100, 1,
            EventLogEmitters.GroundItemEliteTypeThreshold - 1, CancellationToken.None);

        Assert.Single(fake.LoggedEvents);
    }

    [Fact]
    public async Task LogBoxOpenEliteGainAsync_UsesItemUseCategoryAndCode7()
    {
        var fake = new FakeEventLogRepository();

        await fake.LogBoxOpenEliteGainAsync(1, 2, 1035, 9001, 1,
            CancellationToken.None);

        var e = Assert.Single(fake.LoggedEvents);
        Assert.Equal(EventLogEmitters.BoxOpenEliteGainEventCode, e.EventCode);
        Assert.Equal(EventLogCategory.ItemUse, e.Category);
        Assert.Equal(9001, e.ItemId);
        Assert.Equal("Box=1035;EliteReward=9001", e.Payload);
    }

    [Theory]
    [InlineData(true, EventLogEmitters.TradeItemStagedToWindowEventCode)]
    [InlineData(false, EventLogEmitters.TradeItemStagedToInventoryEventCode)]
    public async Task LogTradeItemStagedAsync_PicksCodeByDirection_UnderExistingTradeCategory(bool toWindow,
        short expectedCode)
    {
        var fake = new FakeEventLogRepository();

        await fake.LogTradeItemStagedAsync(1, 2, toWindow, 55, 1, CancellationToken.None);

        var e = Assert.Single(fake.LoggedEvents);
        Assert.Equal(expectedCode, e.EventCode);
        Assert.Equal(EventLogCategory.Trade, e.Category);
    }

    [Theory]
    [InlineData(true, EventLogEmitters.TradeMoneyStagedToWindowEventCode)]
    [InlineData(false, EventLogEmitters.TradeMoneyStagedToInventoryEventCode)]
    public async Task LogTradeMoneyStagedAsync_PicksCodeByDirection(bool toWindow, short expectedCode)
    {
        var fake = new FakeEventLogRepository();

        await fake.LogTradeMoneyStagedAsync(1, 2, toWindow, 500, CancellationToken.None);

        var e = Assert.Single(fake.LoggedEvents);
        Assert.Equal(expectedCode, e.EventCode);
        Assert.Equal(EventLogCategory.Trade, e.Category);
        Assert.Equal(500, e.DeltaMoney);
    }

    [Fact]
    public void TradeStagingCodes_DoNotCollideWithTradeCommitCodes()
    {
        short[] stagingCodes =
        [
            EventLogEmitters.TradeItemStagedToWindowEventCode, EventLogEmitters.TradeItemStagedToInventoryEventCode,
            EventLogEmitters.TradeMoneyStagedToWindowEventCode, EventLogEmitters.TradeMoneyStagedToInventoryEventCode
        ];

        Assert.DoesNotContain((short)1, stagingCodes);
        Assert.DoesNotContain((short)2, stagingCodes);
        Assert.Equal(stagingCodes.Length, stagingCodes.Distinct().Count());
    }

    [Theory]
    [InlineData(true, EventLogEmitters.PetInventoryToPetEventCode)]
    [InlineData(false, EventLogEmitters.PetInventoryFromPetEventCode)]
    public async Task LogPetInventoryTransferAsync_DisambiguatesDirectionViaDistinctEventCodes(bool intoPetBag,
        short expectedCode)
    {
        var fake = new FakeEventLogRepository();

        await fake.LogPetInventoryTransferAsync(1, 2, intoPetBag, 777, 0, CancellationToken.None);

        var e = Assert.Single(fake.LoggedEvents);
        Assert.Equal(expectedCode, e.EventCode);
        Assert.Equal(EventLogCategory.PetInventoryTransfer, e.Category);

        Assert.NotEqual(EventLogEmitters.PetInventoryToPetEventCode, EventLogEmitters.PetInventoryFromPetEventCode);
    }

    [Fact]
    public async Task LogBigMoneyConversionAsync_PreservesNonProportionateDeltasVerbatim()
    {
        var fake = new FakeEventLogRepository();

        await fake.LogBigMoneyConversionAsync(EventLogEmitters.BigMoneyConversionEventCode1, 1,
            2, -1_500_000_000, 1, CancellationToken.None);

        var e = Assert.Single(fake.LoggedEvents);
        Assert.Equal(EventLogCategory.BigMoneyConversion, e.Category);
        Assert.Equal(-1_500_000_000, e.DeltaMoney);
        Assert.Equal(1, e.DeltaBigMoney);
    }

    [Fact]
    public async Task LogBigMoneyConversionAsync_NullAccountId_WritesNullActorAccountId()
    {
        var fake = new FakeEventLogRepository();

        await fake.LogBigMoneyConversionAsync(EventLogEmitters.BigMoneyConversionEventCode5, null,
            10, -5, 5, CancellationToken.None);

        var e = Assert.Single(fake.LoggedEvents);
        Assert.Equal(EventLogEmitters.BigMoneyConversionEventCode5, e.EventCode);
        Assert.Null(e.ActorAccountId);
        Assert.Equal(10, e.ActorCharacterId);
    }

    [Fact]
    public void BigMoneyConversionEventCodes_AreEightDistinctValues()
    {
        short[] codes =
        [
            EventLogEmitters.BigMoneyConversionEventCode1, EventLogEmitters.BigMoneyConversionEventCode2,
            EventLogEmitters.BigMoneyConversionEventCode3, EventLogEmitters.BigMoneyConversionEventCode4,
            EventLogEmitters.BigMoneyConversionEventCode5, EventLogEmitters.BigMoneyConversionEventCode6,
            EventLogEmitters.BigMoneyConversionEventCode7, EventLogEmitters.BigMoneyConversionEventCode8
        ];

        Assert.Equal(8, codes.Distinct().Count());
    }
}
