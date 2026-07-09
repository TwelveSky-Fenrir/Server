using System.Collections.ObjectModel;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Commerce;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Commerce;

/// <summary>
///     Covers <see cref="Zone.MaxProxyShopSlots" /> (<c>MAX_PROXY_SHOP_NUM</c>, Server/Header/Protocol/DEFINE.h:369)
///     as enforced by <see cref="OpenShopStallService.OpenProxyShopAsync" />'s capacity check
///     (Server/ts25zone/S07_MyGame09.cpp:380-392): acceptance with exactly one slot free, rejection with result
///     105 and zero side effects once the global table is full, and that a freed slot -- via either an explicit
///     close (<see cref="CloseShopStallService.CloseOfflineShopAsync" />) or the periodic expiry sweep
///     (<c>Zone.RebroadcastProxyShops</c>) -- is immediately available to a subsequent open.
/// </summary>
/// <remarks>
///     Private, file-scoped fakes (not <c>TestSupport/FakeOfflineShopRepository.cs</c>), same rationale as
///     <see cref="Handlers.Commerce.OpenShopStallServiceProxyRegistrationTests" />'s own remarks: that shared
///     fake is scoped to a different test suite's needs and throws <see cref="NotImplementedException" /> on the
///     members this file exercises.
/// </remarks>
public class OpenShopStallServiceCapacityTests
{
    /// <summary>
    ///     Pumps <see cref="Zone.Tick" /> while <paramref name="pending" /> is outstanding -- needed because
    ///     <see cref="OpenShopStallService.OpenProxyShopAsync" />'s success path awaits
    ///     <c>Zone.PostInventoryCommandAndWaitAsync</c>, which only resolves once this same zone's tick drains
    ///     its inventory-mirror inbox. Same pattern as
    ///     <see cref="Handlers.Commerce.OpenShopStallServiceProxyRegistrationTests.RunToCompletionAsync" />.
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

    /// <summary>
    ///     Registers <paramref name="count" /> distinct filler entries directly against the zone's
    ///     periodic-broadcast table (bypassing the service/repository entirely, since only the table's own
    ///     <see cref="Zone.ProxyShopCount" /> matters for the capacity check under test) using character ids
    ///     <c>1..count</c>, none of which collide with the real characters (ids &gt;= 90000) these tests open
    ///     shops for.
    /// </summary>
    private static void FillZoneToCapacity(Zone zone, int count, int shopDate)
    {
        for (var characterId = 1; characterId <= count; characterId++)
            zone.RegisterProxyShop(new ProxyShopBroadcastEntry(characterId, characterId * 2 + 1, "Filler",
                "FillerStall", characterId, 0f, characterId, shopDate));
    }

    private static async Task<(Zone Zone, PlayerRuntimeState State)> EnterPlayerAsync(Zone zone, int characterId)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, ProxyShopZonePolicy.ZoneNumber, "Seller", 15f, posZ: 25f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        await Task.CompletedTask;
        return (zone, state!);
    }

    [Fact]
    public async Task OpenProxyShopAsync_OneSlotRemaining_Succeeds_AndFillsTheLastSlot()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        FillZoneToCapacity(zone, Zone.MaxProxyShopSlots - 1, GameDate.Today() + 30);
        Assert.Equal(Zone.MaxProxyShopSlots - 1, zone.ProxyShopCount);

        const int characterId = 90001;
        var (_, state) = await EnterPlayerAsync(zone, characterId);

        var offlineShops = new CapacityTrackingOfflineShopRepository();
        var service = new OpenShopStallService(offlineShops, new FixedGameSettingsRepository(7),
            ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(), NullLogger<OpenShopStallService>.Instance);

        var packet = ProxyOpenRequest("LastSlotStall");
        var listing = packet.PshopInfo with { UniqueNumber = unchecked(characterId * 2 + 1) };

        var response = await RunToCompletionAsync(
            service.OpenProxyShopAsync(packet, zone, state, characterId, 1, listing, [], CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(Zone.MaxProxyShopSlots, zone.ProxyShopCount);
        Assert.Equal(characterId, offlineShops.LastOpenedCharacterId);
    }

    [Fact]
    public async Task OpenProxyShopAsync_AtCapacity_RejectsWithResult105_AndHasNoSideEffects()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        FillZoneToCapacity(zone, Zone.MaxProxyShopSlots, GameDate.Today() + 30);
        Assert.Equal(Zone.MaxProxyShopSlots, zone.ProxyShopCount);

        const int characterId = 90002;
        var (_, state) = await EnterPlayerAsync(zone, characterId);

        var offlineShops = new CapacityTrackingOfflineShopRepository();
        var service = new OpenShopStallService(offlineShops, new FixedGameSettingsRepository(7),
            ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(), NullLogger<OpenShopStallService>.Instance);

        var packet = ProxyOpenRequest("OverflowStall");
        var listing = packet.PshopInfo with { UniqueNumber = unchecked(characterId * 2 + 1) };

        // Deliberately NOT run through RunToCompletionAsync: the capacity rejection must return synchronously,
        // never reaching the PostInventoryCommandAndWaitAsync await at all.
        var response = await service.OpenProxyShopAsync(packet, zone, state, characterId, 1, listing, [],
            CancellationToken.None);

        Assert.Equal(105, response.Result);
        Assert.Equal(listing.Name, response.PshopInfo.Name);
        Assert.Equal(listing.UniqueNumber, response.PshopInfo.UniqueNumber);

        // No side effects: table unchanged, no DB write attempted, no audit row logged.
        Assert.Equal(Zone.MaxProxyShopSlots, zone.ProxyShopCount);
        Assert.Null(offlineShops.LastOpenedCharacterId);
        Assert.Equal(0, offlineShops.OpenCallCount);
    }

    [Fact]
    public async Task OpenProxyShopAsync_CapacityFreedByAnExplicitClose_AllowsANewShopToOpen()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        FillZoneToCapacity(zone, Zone.MaxProxyShopSlots, GameDate.Today() + 30);
        Assert.Equal(Zone.MaxProxyShopSlots, zone.ProxyShopCount);

        var offlineShops = new CapacityTrackingOfflineShopRepository();
        var closeService = new CloseShopStallService(offlineShops, NullLogger<CloseShopStallService>.Instance);

        // Frees filler character 1's slot.
        await closeService.CloseOfflineShopAsync(1, zone, CancellationToken.None);
        Assert.Equal(Zone.MaxProxyShopSlots - 1, zone.ProxyShopCount);

        const int characterId = 90003;
        var (_, state) = await EnterPlayerAsync(zone, characterId);

        var service = new OpenShopStallService(offlineShops, new FixedGameSettingsRepository(7),
            ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(), NullLogger<OpenShopStallService>.Instance);

        var packet = ProxyOpenRequest("ReclaimedSlotStall");
        var listing = packet.PshopInfo with { UniqueNumber = unchecked(characterId * 2 + 1) };

        var response = await RunToCompletionAsync(
            service.OpenProxyShopAsync(packet, zone, state, characterId, 1, listing, [], CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(Zone.MaxProxyShopSlots, zone.ProxyShopCount);
    }

    [Fact]
    public async Task OpenProxyShopAsync_CapacityFreedByTheExpirySweep_AllowsANewShopToOpen()
    {
        var zone = ZoneTestKit.CreateZone(ProxyShopZonePolicy.ZoneNumber);
        FillZoneToCapacity(zone, Zone.MaxProxyShopSlots - 1, GameDate.Today() + 30);
        zone.RegisterProxyShop(new ProxyShopBroadcastEntry(999_999, 999_999 * 2 + 1, "ExpiredSeller",
            "ExpiredStall", 0f, 0f, 0f, GameDate.Today() - 1));
        Assert.Equal(Zone.MaxProxyShopSlots, zone.ProxyShopCount);

        // Expiration is checked unconditionally every tick (Zone.RebroadcastProxyShops), independent of the
        // 5s keep-alive throttle -- a single small tick force-closes the already-expired filler.
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(Zone.MaxProxyShopSlots - 1, zone.ProxyShopCount);
        Assert.Contains(999_999, zone.DrainPendingProxyShopCloses());

        const int characterId = 90004;
        var (_, state) = await EnterPlayerAsync(zone, characterId);

        var offlineShops = new CapacityTrackingOfflineShopRepository();
        var service = new OpenShopStallService(offlineShops, new FixedGameSettingsRepository(7),
            ZoneTestKit.EmptyWorldData(), new FakeEventLogRepository(), NullLogger<OpenShopStallService>.Instance);

        var packet = ProxyOpenRequest("PostExpirySlotStall");
        var listing = packet.PshopInfo with { UniqueNumber = unchecked(characterId * 2 + 1) };

        var response = await RunToCompletionAsync(
            service.OpenProxyShopAsync(packet, zone, state, characterId, 1, listing, [], CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(Zone.MaxProxyShopSlots, zone.ProxyShopCount);
    }

    /// <summary>
    ///     Only the two members <see cref="OpenShopStallService.OpenProxyShopAsync" />/
    ///     <see cref="CloseShopStallService" /> call, plus an open-call counter for the no-side-effects
    ///     assertion in the over-capacity rejection test.
    /// </summary>
    private sealed class CapacityTrackingOfflineShopRepository : IOfflineShopRepository
    {
        public List<(int CharacterId, byte ShopState)> ClosedStates { get; } = [];
        public int? LastOpenedCharacterId { get; private set; }
        public int OpenCallCount { get; private set; }

        public ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
            int characterId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ReadOnlyCollection<OfflineShopOpenListingRowDto>> GetAllOpenAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public ValueTask OpenAndReplaceContainersAsync(int characterId, short? zoneNumber, int shopDate,
            string shopName, int locationX, int locationY, int locationZ,
            IReadOnlyList<OfflineShopItemSlotTvp> items, IReadOnlyList<CharacterItemSlotTvp> inventoryPage0,
            IReadOnlyList<CharacterItemSlotTvp> inventoryPage1, CancellationToken ct)
        {
            OpenCallCount++;
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

        public ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney, int todayDate,
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
