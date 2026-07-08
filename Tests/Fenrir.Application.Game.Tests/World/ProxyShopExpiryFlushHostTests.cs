using System.Collections.ObjectModel;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     <see cref="ProxyShopExpiryFlushHost" /> -- the write-behind twin of <c>Zone.RebroadcastProxyShops</c>'s
///     expiry branch: persists the durable ShopState=0 write, then best-effort clears the shop's registered
///     display name (mirroring the legacy account/DB process's own successful-close side effect).
/// </summary>
public class ProxyShopExpiryFlushHostTests
{
    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    private static ProxyShopBroadcastEntry Entry(int characterId, int shopDate)
    {
        return new ProxyShopBroadcastEntry(characterId, characterId * 2 + 1, "Owner", "Shop", 0f, 0f, 0f, shopDate);
    }

    /// <summary>Drives a real expiry through the zone's own sweep rather than reaching into its private queue.</summary>
    private static ZoneRegistry RegistryWithOneExpiredShop(int characterId)
    {
        var registry = CreateRegistry(ProxyShopZonePolicy.ZoneNumber);
        var zone = registry[ProxyShopZonePolicy.ZoneNumber];
        zone.RegisterProxyShop(Entry(characterId, 20200101));
        zone.Tick(SimulationClock.ProxyShopRebroadcastInterval);
        return registry;
    }

    [Fact]
    public async Task FlushOnceAsync_ExpiredShop_PersistsTheCloseAndClearsTheDisplayName()
    {
        var registry = RegistryWithOneExpiredShop(10);
        var repository = new RecordingOfflineShopRepository();
        var host = new ProxyShopExpiryFlushHost(registry, repository, NullLogger<ProxyShopExpiryFlushHost>.Instance);

        await host.FlushOnceAsync(CancellationToken.None);

        Assert.Equal([10], repository.ClosedCharacterIds);
        Assert.Equal([(10, "")], repository.NameClears);
    }

    [Fact]
    public async Task FlushOnceAsync_NothingQueued_NeverCallsTheRepository()
    {
        var registry = CreateRegistry(ProxyShopZonePolicy.ZoneNumber);
        var repository = new RecordingOfflineShopRepository();
        var host = new ProxyShopExpiryFlushHost(registry, repository, NullLogger<ProxyShopExpiryFlushHost>.Instance);

        await host.FlushOnceAsync(CancellationToken.None);

        Assert.Empty(repository.ClosedCharacterIds);
        Assert.Empty(repository.NameClears);
    }

    [Fact]
    public async Task FlushOnceAsync_DisplayNameClearThrows_DoesNotPropagate_AndTheCloseAlreadyPersisted()
    {
        var registry = RegistryWithOneExpiredShop(10);
        var repository = new RecordingOfflineShopRepository { ThrowOnSetProxyShopName = true };
        var host = new ProxyShopExpiryFlushHost(registry, repository, NullLogger<ProxyShopExpiryFlushHost>.Instance);

        await host.FlushOnceAsync(CancellationToken.None); // must not throw

        Assert.Equal([10], repository.ClosedCharacterIds);
        Assert.Empty(repository.NameClears);
    }

    [Fact]
    public async Task FlushOnceAsync_StateWriteThrows_SkipsTheNameClear_AndDoesNotPropagate()
    {
        var registry = RegistryWithOneExpiredShop(10);
        var repository = new RecordingOfflineShopRepository { ThrowOnSetState = true };
        var host = new ProxyShopExpiryFlushHost(registry, repository, NullLogger<ProxyShopExpiryFlushHost>.Instance);

        await host.FlushOnceAsync(CancellationToken.None); // must not throw

        Assert.Empty(repository.ClosedCharacterIds);
        Assert.Empty(repository.NameClears);
    }

    /// <summary>
    ///     Records calls instead of hitting SQL -- deliberately its own local fake rather than extending the
    ///     shared <c>FakeOfflineShopRepository</c> (that one only implements the rental-extension surface used
    ///     by <c>UseInventoryItemServiceTests</c>).
    /// </summary>
    private sealed class RecordingOfflineShopRepository : IOfflineShopRepository
    {
        public List<int> ClosedCharacterIds { get; } = [];
        public List<(int CharacterId, string ShopName)> NameClears { get; } = [];
        public bool ThrowOnSetState { get; set; }
        public bool ThrowOnSetProxyShopName { get; set; }

        public ValueTask<(OfflineShopRowDto? Shop, IReadOnlyList<OfflineShopItemRowDto> Items)> GetByCharacterAsync(
            int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ReadOnlyCollection<OfflineShopOpenListingRowDto>> GetAllOpenAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask OpenAndReplaceContainersAsync(int characterId, short? zoneNumber, int shopDate,
            string shopName, int locationX, int locationY, int locationZ,
            IReadOnlyList<OfflineShopItemSlotTvp> items, IReadOnlyList<CharacterItemSlotTvp> inventoryPage0,
            IReadOnlyList<CharacterItemSlotTvp> inventoryPage1, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetStateAsync(int characterId, byte shopState, CancellationToken ct)
        {
            if (ThrowOnSetState)
                throw new InvalidOperationException("Simulated SQL failure");

            ClosedCharacterIds.Add(characterId);
            return ValueTask.CompletedTask;
        }

        public ValueTask RetrieveItemAndReplaceContainerAsync(int characterId, short slotIndex, int expectedItemId,
            int expectedQuantity, int expectedValue, byte container, IReadOnlyList<CharacterItemSlotTvp> items,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask ExecutePurchaseAsync(int sellerCharacterId, short slotIndex, int expectedItemId,
            int expectedQuantity, int expectedValue, int price, int buyerCharacterId, byte buyerContainer,
            IReadOnlyList<CharacterItemSlotTvp> buyerItems, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask WithdrawMoneyAsync(int characterId, int expectedMoney, int expectedBigMoney, int todayDate,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ProxyShopNameRowDto?> GetProxyShopNameAsync(int characterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask SetProxyShopNameAsync(int characterId, string shopName, CancellationToken ct)
        {
            if (ThrowOnSetProxyShopName)
                throw new InvalidOperationException("Simulated SQL failure");

            NameClears.Add((characterId, shopName));
            return ValueTask.CompletedTask;
        }

        public ValueTask ExtendRentalAsync(int characterId, int newShopDate, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}
