using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Commerce;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Commerce;

public class OpenShopStallServiceVaultExpiryTests
{
    private const int ListedItemId = 500;
    private static readonly ItemStack ListedStack = new(ListedItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 123);

    private static async Task<PlayerRuntimeState> EnterPlayerAsync(int characterId)
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, "Seller", 15f, posZ: 25f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        state!.ActionSort = 1;

        await Task.CompletedTask;
        return state;
    }

    private static void SeedLiveSlot(PlayerRuntimeState state, byte page, byte index, ItemStack? stack)
    {
        var updated = stack is { } s
            ? state.Inventory.GetContainer(page).SetItem(index, s)
            : state.Inventory.GetContainer(page).Remove(index);
        state.Inventory.ReplaceContainer(page, updated);
    }

    private static OpenShopStallRequest RequestWithOneOccupiedSlot(byte inventoryPage, byte inventoryIndex)
    {
        var itemInfo = new int[225];
        var i = PshopPurchasePolicy.FlatIndex(0, 0);
        itemInfo[i] = ListedItemId;
        itemInfo[i + 1] = 1;
        itemInfo[i + 2] = 0;
        itemInfo[i + 3] = 0;
        itemInfo[i + 4] = 1;
        itemInfo[i + 5] = inventoryPage;
        itemInfo[i + 6] = inventoryIndex;
        itemInfo[i + 7] = 0;
        itemInfo[i + 8] = 0;

        return new OpenShopStallRequest
        {
            Sort = 2,
            PshopInfo = new PshopInfo
            {
                UniqueNumber = 0, Name = "VaultGateStall", ItemInfo = itemInfo, SocketInfo = new int[75]
            }
        };
    }

    private static OpenShopStallService CreateService()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [ListedItemId] = new(WorldDataTestRows.Item(ListedItemId), [])
        }.ToFrozenDictionary();

        return new OpenShopStallService(new FakeOfflineShopRepository(), new UnusedGameSettingsRepository(),
            ZoneTestKit.EmptyWorldData(itemsById), new FakeEventLogRepository(),
            NullLogger<OpenShopStallService>.Instance);
    }

    [Fact]
    public async Task LastPageExpired_SlotReferencingItAborts_EvenThoughTheLiveSlotMatchesExactly()
    {
        var state = await EnterPlayerAsync(90101);
        state.InventoryDate = 20200101;
        SeedLiveSlot(state, ContainerMatrix.InventoryPage1, 5, ListedStack);

        var service = CreateService();
        var packet = RequestWithOneOccupiedSlot(ContainerMatrix.InventoryPage1, 5);

        var result = await service.PrepareAsync(packet, state, CancellationToken.None);

        Assert.Equal(OpenShopStallPrepareOutcome.Abort, result.Outcome);
    }

    [Fact]
    public async Task LastPageStillValid_SlotReferencingItPasses_ReachesProxyReady()
    {
        var state = await EnterPlayerAsync(90102);
        state.InventoryDate = 99991231;
        SeedLiveSlot(state, ContainerMatrix.InventoryPage1, 5, ListedStack);

        var service = CreateService();
        var packet = RequestWithOneOccupiedSlot(ContainerMatrix.InventoryPage1, 5);

        var result = await service.PrepareAsync(packet, state, CancellationToken.None);

        Assert.Equal(OpenShopStallPrepareOutcome.ProxyReady, result.Outcome);
        Assert.NotNull(result.OfflineItems);
        Assert.Single(result.OfflineItems!);
    }

    [Fact]
    public async Task FirstPage_NeverBlockedByVaultExpiry_RegardlessOfInventoryDate()
    {
        var state = await EnterPlayerAsync(90103);
        state.InventoryDate = 20200101;
        SeedLiveSlot(state, ContainerMatrix.InventoryPage0, 5, ListedStack);

        var service = CreateService();
        var packet = RequestWithOneOccupiedSlot(ContainerMatrix.InventoryPage0, 5);

        var result = await service.PrepareAsync(packet, state, CancellationToken.None);

        Assert.Equal(OpenShopStallPrepareOutcome.ProxyReady, result.Outcome);
    }

    private sealed class UnusedGameSettingsRepository : IGameSettingsRepository
    {
        public ValueTask<GameSettingsDto> GetAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
