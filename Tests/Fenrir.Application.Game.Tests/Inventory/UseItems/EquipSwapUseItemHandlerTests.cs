using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

public class EquipSwapUseItemHandlerTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const int ItemId = 9000;

    private static (Zone Zone, ZoneClientSession Session, PlayerRuntimeState State, EquipSwapUseItemHandler Handler)
        SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1, worldData: ZoneTestKit.EmptyWorldData());
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1, level: 100)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));

        var writer = new UseItemInventoryWriter(new FakeCharacterRepository(),
            NullLogger<UseItemInventoryWriter>.Instance);
        var handler = new EquipSwapUseItemHandler(ZoneTestKit.EmptyWorldData(), writer,
            NullLogger<EquipSwapUseItemHandler>.Instance);

        return (zone, session, state!, handler);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, ItemRowDto row)
    {
        var item = new ItemStack(row.ItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        return new UseItemContext(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, item,
            new ItemDefinition(row, []), 0);
    }

    [Fact]
    public async Task NonIdlePosture_Disconnects_SameSeverityAsEveryOtherGuardInThisCluster()
    {
        var (zone, session, state, handler) = SetUp();
        state.ActionSort = 2;
        var row = WorldDataTestRows.Item(ItemId) with { Sort = 6, EquipInfo1 = 1, EquipInfo2 = 2 };

        await handler.HandleAsync(Context(zone, state, row), CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task FailsEquipEligibility_Disconnects_SameSeverityAsNonIdlePosture()
    {
        var (zone, session, state, handler) = SetUp();
        state.ActionSort = 1;
        var row = WorldDataTestRows.Item(ItemId) with { Sort = 6, EquipInfo1 = 0, EquipInfo2 = 2 };

        await handler.HandleAsync(Context(zone, state, row), CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task EquipPartTagMapsToNoRealSlot_Disconnects_SameSeverityAsTheOtherTwoGuards()
    {
        var (zone, session, state, handler) = SetUp();
        state.ActionSort = 1;
        var row = WorldDataTestRows.Item(ItemId) with { Sort = 6, EquipInfo1 = 1, EquipInfo2 = 1 };

        await handler.HandleAsync(Context(zone, state, row), CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }
}
