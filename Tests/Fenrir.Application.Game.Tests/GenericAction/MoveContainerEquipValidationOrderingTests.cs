using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.GenericAction;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.GenericAction;

public class MoveContainerEquipValidationOrderingTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short ZoneNumber = 1;
    private const int EquipmentSlotIndex = 2;
    private const byte MatchingEquipPartTag = 4;
    private const int MismatchedTribeItemId = 70000;
    private const int MatchingTribeItemId = 70001;
    private const int OccupantItemId = 70002;

    private static WorldDataCache BuildWorldData()
    {
        var mismatchedTribeItem = WorldDataTestRows.Item(MismatchedTribeItemId) with
        {
            Sort = 6, EquipInfo1 = 5, EquipInfo2 = MatchingEquipPartTag
        };
        var matchingTribeItem = WorldDataTestRows.Item(MatchingTribeItemId) with
        {
            Sort = 6, EquipInfo1 = EquipItemValidationGate.AnyTribeSentinel, EquipInfo2 = MatchingEquipPartTag
        };

        return ZoneTestKit.EmptyWorldData(new[] { mismatchedTribeItem, matchingTribeItem }
            .Select(row => new ItemDefinition(row, []))
            .ToFrozenDictionary(d => d.Item.ItemId));
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters) SetUp(
        WorldDataCache worldData, int sourceItemId)
    {
        var zone = ZoneTestKit.CreateZone(ZoneNumber, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, ZoneNumber)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));

        state!.PreviousTribe = 0;
        state.Level = 100;
        state.Level2 = 0;
        state.RebirthCount = 0;
        state.ActionSort = 1;

        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            state.Inventory.GetContainer(ContainerMatrix.InventoryPage0)
                .SetItem(5, new ItemStack(sourceItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)));

        return (zone, state, new FakeCharacterRepository());
    }

    private static GenericActionService CreateService(WorldDataCache worldData, FakeCharacterRepository characters)
    {
        return new GenericActionService(characters, worldData, new QuestCatalog(worldData), new PartyRegistry(),
            new FakeEventLogRepository(), new FakeAccountVaultRepository(), new TradeRegistry(),
            ZoneTestKit.CreateRegistry(), NullLogger<GenericActionService>.Instance);
    }

    private static byte[] InventoryToEquipMoveBytes()
    {
        var move = new DefaultPData
        {
            Page1 = ContainerMatrix.InventoryPage0, Index1 = 5, Quantity1 = 0,
            Page2 = 0, Index2 = EquipmentSlotIndex, XPost2 = 0, YPost2 = 0
        };
        var buffer = new byte[DefaultPData.WireSize];
        move.Write(buffer);
        return buffer;
    }

    private static void OccupyEquipmentSlot(PlayerRuntimeState state)
    {
        state.Inventory.ReplaceContainer(ContainerMatrix.Equipment,
            state.Inventory.GetContainer(ContainerMatrix.Equipment)
                .SetItem(EquipmentSlotIndex, new ItemStack(OccupantItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
    }

    [Fact]
    public async Task MoveContainerAsync_EquipValidationFails_DestinationAlsoOccupied_AbortsRatherThanFails()
    {
        var worldData = BuildWorldData();
        var (zone, state, characters) = SetUp(worldData, MismatchedTribeItemId);
        OccupyEquipmentSlot(state);
        var service = CreateService(worldData, characters);

        var result = await service.MoveContainerAsync(210, InventoryToEquipMoveBytes(), zone, state, CharacterId,
            CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, result.Status);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task MoveContainerAsync_EquipValidationFails_DestinationEmpty_StillAborts()
    {
        var worldData = BuildWorldData();
        var (zone, state, characters) = SetUp(worldData, MismatchedTribeItemId);
        var service = CreateService(worldData, characters);

        var result = await service.MoveContainerAsync(210, InventoryToEquipMoveBytes(), zone, state, CharacterId,
            CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, result.Status);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task MoveContainerAsync_EquipValidationSucceeds_DestinationOccupied_AbortsAsHardReject()
    {
        var worldData = BuildWorldData();
        var (zone, state, characters) = SetUp(worldData, MatchingTribeItemId);
        OccupyEquipmentSlot(state);
        var service = CreateService(worldData, characters);

        var result = await service.MoveContainerAsync(210, InventoryToEquipMoveBytes(), zone, state, CharacterId,
            CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, result.Status);
        Assert.Null(characters.LastReplacedContainer);
    }
}
