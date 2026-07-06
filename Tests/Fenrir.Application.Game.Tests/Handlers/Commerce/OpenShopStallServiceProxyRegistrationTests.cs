using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Commerce;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Commerce;

/// <summary>
///     Covers the <see cref="Zone.RegisterProxyShop" />/<see cref="Zone.RemoveProxyShop" /> wiring added to
///     <see cref="OpenShopStallService" />/<see cref="CloseShopStallService" /> so a durably-opened/closed
///     proxy shop enters/leaves <see cref="Zone" />'s periodic-broadcast table (<c>Zone.ProxyShops.cs</c>) --
///     not the DB round trips themselves, which have their own coverage elsewhere.
/// </summary>
/// <remarks>
///     Private, file-scoped fakes (not <c>TestSupport/FakeOfflineShopRepository.cs</c>): that shared fake is
///     already scoped to a different test suite's (proxy-shop rental-extension) needs and throws
///     <see cref="NotImplementedException" /> on the two members this file actually exercises.
/// </remarks>
public class OpenShopStallServiceProxyRegistrationTests
{
    /// <summary>
    ///     Pumps <see cref="Zone.Tick" /> while <paramref name="pending" /> is outstanding -- needed because
    ///     <see cref="OpenShopStallService.OpenProxyShopAsync" />'s success path awaits
    ///     <c>Zone.PostInventoryCommandAndWaitAsync</c>, which only resolves once this same zone's tick drains
    ///     its inventory-mirror inbox. Same pattern as <c>UseInventoryItemServiceTests.RunToCompletionAsync</c>.
    /// </summary>
    private static async Task<OpenShopStallResponse> RunToCompletionAsync(ValueTask<OpenShopStallResponse> pending,
        Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("OpenProxyShopAsync task never completed.");
        }

        return await task;
    }

    private static OpenShopStallRequest ProxyOpenRequest(string shopName)
    {
        return new OpenShopStallRequest
        {
            Sort = 2,
            PshopInfo = new PshopInfo
            {
                UniqueNumber = 0,
                Name = shopName,
                ItemInfo = new int[225],
                SocketInfo = new int[75]
            }
        };
    }

    [Fact]
    public async Task OpenProxyShopAsync_RegistersTheShopWithTheZone_OnSuccess()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, _) = ZoneTestKit.CreateSession(1);
        const int characterId = 42;

        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, "Seller", 15f, posZ: 25f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));

        var offlineShops = new OpenTrackingOfflineShopRepository();
        var gameSettings = new FixedGameSettingsRepository(7);
        var service = new OpenShopStallService(offlineShops, gameSettings, ZoneTestKit.EmptyWorldData(),
            NullLogger<OpenShopStallService>.Instance);

        var packet = ProxyOpenRequest("MyStall");
        var listing = packet.PshopInfo with { UniqueNumber = unchecked(characterId * 2 + 1) };

        var response = await RunToCompletionAsync(
            service.OpenProxyShopAsync(packet, zone, state!, characterId, listing, [], CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(1, zone.ProxyShopCount);
        Assert.NotNull(offlineShops.LastOpenedCharacterId);
        Assert.Equal(characterId, offlineShops.LastOpenedCharacterId);
    }

    [Fact]
    public async Task OpenProxyShopAsync_DoesNotRegisterWithTheZone_WhenTheDurableOpenFails()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        var (session, _) = ZoneTestKit.CreateSession(1);
        const int characterId = 43;

        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, "Seller", 15f, posZ: 25f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));

        var offlineShops = new OpenTrackingOfflineShopRepository { ThrowOnOpen = true };
        var gameSettings = new FixedGameSettingsRepository(7);
        var service = new OpenShopStallService(offlineShops, gameSettings, ZoneTestKit.EmptyWorldData(),
            NullLogger<OpenShopStallService>.Instance);

        var packet = ProxyOpenRequest("MyStall");
        var listing = packet.PshopInfo with { UniqueNumber = unchecked(characterId * 2 + 1) };

        var response = await service.OpenProxyShopAsync(packet, zone, state!, characterId, listing, [],
            CancellationToken.None);

        Assert.Equal(102, response.Result);
        Assert.Equal(0, zone.ProxyShopCount);
    }

    [Fact]
    public async Task CloseOfflineShopAsync_RemovesTheShopFromTheZonesBroadcastTable()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        const int characterId = 44;

        zone.RegisterProxyShop(new ProxyShopBroadcastEntry(characterId, characterId * 2 + 1, "Seller", "MyStall",
            10f, 0f, 10f, GameDate.Today()));
        Assert.Equal(1, zone.ProxyShopCount);

        var offlineShops = new OpenTrackingOfflineShopRepository();
        var service = new CloseShopStallService(offlineShops);

        await service.CloseOfflineShopAsync(characterId, zone, CancellationToken.None);

        Assert.Equal(0, zone.ProxyShopCount);
        Assert.Contains((characterId, (byte)0), offlineShops.ClosedStates);
    }

    /// <summary>
    ///     Only the two members <see cref="OpenShopStallService.OpenProxyShopAsync" />/
    ///     <see cref="CloseShopStallService" /> call.
    /// </summary>
    private sealed class OpenTrackingOfflineShopRepository : IOfflineShopRepository
    {
        public List<(int CharacterId, byte ShopState)> ClosedStates { get; } = [];
        public int? LastOpenedCharacterId { get; private set; }
        public bool ThrowOnOpen { get; set; }

        public ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
            int characterId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public ValueTask OpenAndReplaceContainersAsync(int characterId, short? zoneNumber, int shopDate,
            string shopName, int locationX, int locationY, int locationZ,
            IReadOnlyList<OfflineShopItemSlotTvp> items, IReadOnlyList<CharacterItemSlotTvp> inventoryPage0,
            IReadOnlyList<CharacterItemSlotTvp> inventoryPage1, CancellationToken ct)
        {
            if (ThrowOnOpen)
                throw new InvalidOperationException("Simulated SQL failure");

            LastOpenedCharacterId = characterId;
            return ValueTask.CompletedTask;
        }

        public ValueTask SetStateAsync(int characterId, byte shopState, CancellationToken ct)
        {
            ClosedStates.Add((characterId, shopState));
            return ValueTask.CompletedTask;
        }

        public ValueTask RetrieveItemAndReplaceContainerAsync(int characterId, short slotIndex, int expectedItemId,
            int expectedQuantity, int expectedValue, byte container, IReadOnlyList<CharacterItemSlotTvp> items,
            CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public ValueTask ExecutePurchaseAsync(int sellerCharacterId, short slotIndex, int expectedItemId,
            int expectedQuantity, int expectedValue, int price, int buyerCharacterId, byte buyerContainer,
            IReadOnlyList<CharacterItemSlotTvp> buyerItems, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney,
            CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ProxyShopNameRowDto?> GetProxyShopNameAsync(int characterId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public ValueTask SetProxyShopNameAsync(int characterId, string shopName, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public ValueTask ExtendRentalAsync(int characterId, int newShopDate, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FixedGameSettingsRepository(byte proxyShopDurationDays) : IGameSettingsRepository
    {
        public ValueTask<GameSettingsDto> GetAsync(CancellationToken ct)
        {
            return ValueTask.FromResult(new GameSettingsDto(proxyShopDurationDays));
        }
    }
}
