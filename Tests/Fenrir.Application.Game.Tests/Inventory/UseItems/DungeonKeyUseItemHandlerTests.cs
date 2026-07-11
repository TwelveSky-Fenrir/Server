using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

/// <summary>
///     Drives <see cref="DungeonKeyUseItemHandler" /> (op23 item 1048) directly -- not through
///     <see cref="UseItemHandlerRegistry" />, since wiring the handler into that registry's constructor is a
///     verbatim edit to an existing file reported separately (see this workstream's wiringManifest).
///     <para>
///         WIRING-PENDING: this test file (like the handler it drives) references
///         <c>TribeProgressZoneCommand.DungeonKeyTime</c>, a field that does not exist on that record as of
///         this file's introduction -- it will not compile until this workstream's wiringManifest is applied.
///     </para>
/// </summary>
public class DungeonKeyUseItemHandlerTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;

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
                throw new TimeoutException("DungeonKeyUseItemHandler task never completed.");
        }

        return await task;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        DungeonKeyUseItemHandler Handler) SetUp()
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
        var handler = new DungeonKeyUseItemHandler(writer, eventLog, NullLogger<DungeonKeyUseItemHandler>.Instance);

        return (zone, state!, characters, handler);
    }

    private static ItemStack Ticket(int quantity = 1)
    {
        return new ItemStack(DungeonKeyUseItemHandler.ItemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, ItemStack item)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, 0, 0, item,
            new ItemDefinition(WorldDataTestRows.Item(DungeonKeyUseItemHandler.ItemId), []), 0);
    }

    [Fact]
    public async Task Use_AddsOne_ConsumesOneUnit()
    {
        var (zone, state, characters, handler) = SetUp();
        state.DungeonKeyTime = 5;
        var item = Ticket();

        var response = await RunToCompletionAsync(handler.HandleAsync(Context(zone, state, item), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(6, response.Value);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(6, after!.DungeonKeyTime);
        Assert.NotNull(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task WouldExceedCeiling_FailsCleanly_NoConsumption()
    {
        var (zone, state, characters, handler) = SetUp();
        state.DungeonKeyTime = BankedCounterMath.GlobalCeiling;
        var item = Ticket();

        var response = await handler.HandleAsync(Context(zone, state, item), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Equal(BankedCounterMath.GlobalCeiling, state.DungeonKeyTime);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task ZeroStackQuantity_FailsCleanly()
    {
        var (zone, state, characters, handler) = SetUp();
        var item = Ticket(quantity: 0);

        var response = await handler.HandleAsync(Context(zone, state, item), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
    }
}
