using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Covers the Rebirth Pill (world.Items 632/1241/2462) -- Path A of the G1-G12 rebirth-advancement
///     mechanism, the only path that reaches generations 7-12. Kept in its own file, separate from
///     <c>UseInventoryItemConsumablesServiceTests</c>/<c>UseInventoryItemServiceTests</c>, for the same
///     "avoid two concurrently-evolving suites touching shared setup" reason those two already give.
///     <para>
///         The per-tick anti-flood throttle (<see cref="PlayerRuntimeState.LastItemUseUtc" />) is enforced by
///         <c>UseInventoryItemHandler</c> before any item-specific dispatch, one layer above
///         <see cref="UseInventoryItemService.ResolveAsync" /> -- out of scope for this file, which drives the
///         service directly.
///     </para>
/// </summary>
public class UseInventoryItemRebirthPillServiceTests
{
    private const byte SpecialUseSort = 3;
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const int RebirthPillItemIdA = 632;
    private const int RebirthPillItemIdB = 1241;
    private const int RebirthPillItemIdC = 2462;

    // The threshold ReturnHighExpValue(12) resolves to -- see RebirthProgression.HighLevelExpTable's own remarks.
    private const int MaxHighLevelExp = 1_481_117_817;

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
                throw new TimeoutException("UseInventoryItemService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters) SetUp(short level2 = RebirthProgression.MaxHighLevel,
            int exp2 = MaxHighLevelExp, int rebirthCount = 0)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        state!.Level2 = level2;
        state.Exp2 = exp2;
        state.RebirthCount = rebirthCount;
        return (session, pipe, zone, state, new FakeCharacterRepository());
    }

    private static void SeedInventory(Zone zone, ItemStack item)
    {
        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, item)));
        zone.PostInventoryCommand(new InventoryZoneCommand(CharacterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static UseInventoryItemService CreateService(FakeCharacterRepository characters)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [RebirthPillItemIdA] =
                new(WorldDataTestRows.Item(RebirthPillItemIdA) with { Sort = SpecialUseSort }, []),
            [RebirthPillItemIdB] =
                new(WorldDataTestRows.Item(RebirthPillItemIdB) with { Sort = SpecialUseSort }, []),
            [RebirthPillItemIdC] =
                new(WorldDataTestRows.Item(RebirthPillItemIdC) with { Sort = SpecialUseSort }, [])
        }.ToFrozenDictionary();

        return new UseInventoryItemService(characters, new FakeGuildRepository(), new FakeCashRepository(),
            new FakeOfflineShopRepository(), new FakeEventLogRepository(), ZoneTestKit.EmptyWorldData(itemsById),
            NullLogger<UseInventoryItemService>.Instance);
    }

    [Theory]
    [InlineData(RebirthPillItemIdA)]
    [InlineData(RebirthPillItemIdB)]
    [InlineData(RebirthPillItemIdC)]
    public async Task RebirthPill_AnyOfTheThreeInterchangeableIds_Succeeds_ResetsExp2_AndIncrementsRebirthCount(
        int itemId)
    {
        var (session, _, zone, state, characters) = SetUp(rebirthCount: 3);
        SeedInventory(zone, new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(4, after!.RebirthCount);
        Assert.Equal(0, after.Exp2);
        // Path A never touches ContributionPoints/Zone241Time/HP/MP -- unlike Path B (TribeActionService.RebirthAsync).
        Assert.Equal(0, after.ContributionPoints);
        Assert.Equal(0, after.Zone241Time);
    }

    [Fact]
    public async Task RebirthPill_ConsumesExactlyOneUnit_LeavesRemainderInSlot_WhenStackHasMoreThanOne()
    {
        var (_, _, zone, state, characters) = SetUp();
        SeedInventory(zone, new ItemStack(RebirthPillItemIdA, 3, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        var remaining = after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0);
        Assert.NotNull(remaining);
        Assert.Equal(2, remaining!.Value.Quantity);
    }

    [Fact]
    public async Task RebirthPill_ConsumesTheLastUnit_ClearsTheSlotEntirely()
    {
        var (_, _, zone, state, characters) = SetUp();
        SeedInventory(zone, new ItemStack(RebirthPillItemIdA, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Fact]
    public async Task RebirthPill_NoItemInReferencedSlot_FailsCleanly()
    {
        var (session, _, zone, state, characters) = SetUp();
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task RebirthPill_Level2NotAtCap_FailsCleanly_LeavesStateAndItemUntouched()
    {
        var (session, _, zone, state, characters) = SetUp(RebirthProgression.MaxHighLevel - 1);
        SeedInventory(zone, new ItemStack(RebirthPillItemIdA, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Equal(0, state.RebirthCount);
        Assert.NotNull(state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Fact]
    public async Task RebirthPill_Exp2BelowFullThreshold_FailsCleanly_LeavesStateAndItemUntouched()
    {
        var (session, _, zone, state, characters) = SetUp(exp2: MaxHighLevelExp - 1);
        SeedInventory(zone, new ItemStack(RebirthPillItemIdA, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.NotNull(state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Fact]
    public async Task RebirthPill_RebirthCountAlreadyAtAbsoluteCap_FailsCleanly()
    {
        var (session, _, zone, state, characters) =
            SetUp(rebirthCount: RebirthProgression.MaxRebirthGeneration);
        SeedInventory(zone, new ItemStack(RebirthPillItemIdA, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Equal(RebirthProgression.MaxRebirthGeneration, state.RebirthCount);
    }

    /// <summary>
    ///     Reaches generation 12 -- unlike Path B (TribeActionService.RebirthAsync, hard-capped at 6), Path A
    ///     is the only route past generation 6.
    /// </summary>
    [Fact]
    public async Task RebirthPill_FromGeneration11_ReachesTheAbsoluteCapOf12()
    {
        var (session, _, zone, state, characters) =
            SetUp(rebirthCount: RebirthProgression.MaxRebirthGeneration - 1);
        SeedInventory(zone, new ItemStack(RebirthPillItemIdA, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(RebirthProgression.MaxRebirthGeneration, after!.RebirthCount);
    }

    /// <summary>
    ///     Proves rebirth cannot be repeated without consuming the required item each time: a second attempt
    ///     against the now-emptied slot fails the generic item-lookup precondition, the same "no double-spend"
    ///     guarantee every other family in this file relies on (DecreaseQunatity clears the whole slot at zero).
    /// </summary>
    [Fact]
    public async Task RebirthPill_SecondAttemptAgainstTheSameNowEmptySlot_FailsCleanly_NoDoubleSpend()
    {
        var (session, _, zone, state, characters) = SetUp(rebirthCount: 0);
        SeedInventory(zone, new ItemStack(RebirthPillItemIdA, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var service = CreateService(characters);

        var first = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);
        Assert.Equal(0, first.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var afterFirst));
        Assert.Equal(1, afterFirst!.RebirthCount);
        Assert.Null(afterFirst.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));

        // Exp2 was reset to 0 by the first success too, but the item-lookup precondition is checked first, so
        // this fails there rather than on the Exp2 threshold -- the slot genuinely holds nothing to consume.
        var second = await RunToCompletionAsync(
            service.ResolveAsync(zone, afterFirst, CharacterId, AccountId, ContainerMatrix.InventoryPage0, 0, 0,
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, second.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var afterSecond));
        Assert.Equal(1, afterSecond!.RebirthCount); // unchanged by the rejected second attempt
    }
}
