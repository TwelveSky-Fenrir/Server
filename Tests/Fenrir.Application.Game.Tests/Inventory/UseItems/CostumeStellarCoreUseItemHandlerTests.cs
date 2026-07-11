using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

public class CostumeStellarCoreUseItemHandlerTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;

    private const int ValidCostumeItemId = 301;
    private const int OtherValidCostumeItemId = 302;
    private const int ValidStellarCoreItemId = 76527;
    private const int OtherValidStellarCoreItemId = 76528;
    private const int UnwhitelistedItemId = 999999;

    private static async Task<UseInventoryItemResponse> RunToCompletionAsync(
        ValueTask<UseInventoryItemResponse> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("CostumeStellarCoreUseItemHandler task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        FakeEventLogRepository EventLog, CostumeStellarCoreUseItemHandler Handler) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));

        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var writer = new UseItemInventoryWriter(characters, NullLogger<UseItemInventoryWriter>.Instance);
        var handler = new CostumeStellarCoreUseItemHandler(writer, eventLog,
            NullLogger<CostumeStellarCoreUseItemHandler>.Instance);

        return (session, zone, state!, characters, eventLog, handler);
    }

    private static ItemStack Item(int itemId, byte enchant = 0, byte combine = 0, byte refine = 0, byte socket = 0,
        int expireDate = 0)
    {
        return new ItemStack(itemId, 1, enchant, combine, refine, socket, 0, 0, 0, expireDate, 1);
    }

    private static ItemDefinition Definition(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId), []);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, ItemStack item)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, 0, 0, item, Definition(item.ItemId), 0);
    }


    [Fact]
    public void ClaimsItem_ValidCostumeId_ReturnsTrue()
    {
        Assert.True(CostumeStellarCoreUseItemHandler.ClaimsItem(ValidCostumeItemId));
    }

    [Fact]
    public void ClaimsItem_ValidStellarCoreId_ReturnsTrue()
    {
        Assert.True(CostumeStellarCoreUseItemHandler.ClaimsItem(ValidStellarCoreItemId));
    }

    [Fact]
    public void ClaimsItem_UnwhitelistedId_ReturnsFalse()
    {
        Assert.False(CostumeStellarCoreUseItemHandler.ClaimsItem(UnwhitelistedItemId));
    }


    [Fact]
    public async Task CostumeGrant_FirstFreeSlot_GrantsItem_CopiesDateAndExpireDate_ConsumesSourceItem()
    {
        var (session, zone, state, characters, eventLog, handler) = SetUp();
        var item = Item(ValidCostumeItemId, enchant: 5, combine: 1, refine: 2, socket: 0, expireDate: 12345);

        var response =
            await RunToCompletionAsync(handler.HandleAsync(Context(zone, state, item), CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.Equal(0, response.Value);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(ValidCostumeItemId, after!.CostumeWardrobe[0]);
        Assert.Equal(12345, after.CostumeExpireDate[0]);
        Assert.NotEqual(0, after.CostumeDate[0]);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.Equal(2, eventLog.LoggedEvents.Count);
    }

    [Fact]
    public async Task CostumeGrant_UsesFirstFreeSlot_NotNecessarilySlotZero()
    {
        var (_, zone, state, _, _, handler) = SetUp();
        state.CostumeWardrobe = state.CostumeWardrobe.SetItem(0, 999);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, Item(ValidCostumeItemId)), CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, response.Result);
        Assert.Equal(1, response.Value);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(ValidCostumeItemId, after!.CostumeWardrobe[1]);
    }

    [Fact]
    public async Task CostumeGrant_AlreadyWorn_FailsCleanly_NoConsumptionNoDisconnect()
    {
        var (session, zone, state, characters, eventLog, handler) = SetUp();
        state.CostumeWardrobe = state.CostumeWardrobe.SetItem(3, ValidCostumeItemId);

        var response = await handler.HandleAsync(Context(zone, state, Item(ValidCostumeItemId)),
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task CostumeGrant_AllTenSlotsFull_Disconnects_NoConsumption()
    {
        var (session, zone, state, characters, eventLog, handler) = SetUp();
        for (var slot = 0; slot < 10; slot++)
            state.CostumeWardrobe = state.CostumeWardrobe.SetItem(slot, OtherValidCostumeItemId + slot * 10);

        await handler.HandleAsync(Context(zone, state, Item(ValidCostumeItemId)), CancellationToken.None);

        Assert.Equal(DisconnectReason.WardrobeFull, session.DisconnectReason);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }


    [Fact]
    public async Task StellarCoreGrant_FirstFreeSlot_GrantsItem_CopiesExpireDate_ConsumesSourceItem()
    {
        var (session, zone, state, characters, eventLog, handler) = SetUp();
        var item = Item(ValidStellarCoreItemId, expireDate: 54321);

        var response =
            await RunToCompletionAsync(handler.HandleAsync(Context(zone, state, item), CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.Equal(0, response.Value);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(ValidStellarCoreItemId, after!.StellarCoreWardrobe[0]);
        Assert.Equal(54321, after.StellarCoreExpireDate[0]);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.Equal(2, eventLog.LoggedEvents.Count);
    }

    [Fact]
    public async Task StellarCoreGrant_AlreadyWorn_FailsCleanly_NoConsumptionNoDisconnect()
    {
        var (session, zone, state, characters, _, handler) = SetUp();
        state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(2, ValidStellarCoreItemId);

        var response = await handler.HandleAsync(Context(zone, state, Item(ValidStellarCoreItemId)),
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task StellarCoreGrant_AllTenSlotsFull_Disconnects_NoConsumption()
    {
        var (session, zone, state, characters, eventLog, handler) = SetUp();
        for (var slot = 0; slot < 10; slot++)
            state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(slot, OtherValidStellarCoreItemId + slot);

        await handler.HandleAsync(Context(zone, state, Item(ValidStellarCoreItemId)), CancellationToken.None);

        Assert.Equal(DisconnectReason.WardrobeFull, session.DisconnectReason);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(eventLog.LoggedEvents);
    }


    [Fact]
    public async Task CheckOrder_CostumeCheckedBeforeStellarCore_ForADisjointId()
    {
        var (_, zone, state, _, _, handler) = SetUp();

        await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, Item(ValidCostumeItemId)), CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.DoesNotContain(ValidCostumeItemId, after!.StellarCoreWardrobe);
    }
}
