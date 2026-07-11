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
///     Drives <see cref="EliteDungeonTicketUseItemHandler" /> (op23 item 1047/1097/1098) directly -- not
///     through <see cref="UseItemHandlerRegistry" />, since wiring the handler into that registry's
///     constructor is a verbatim edit to an existing file reported separately (see this workstream's
///     wiringManifest).
///     <para>
///         WIRING-PENDING: this test file (like the handler it drives) references
///         <c>TribeProgressZoneCommand.EliteDungeonTime</c>, a field that does not exist on that record as of
///         this file's introduction -- it will not compile until this workstream's wiringManifest is applied.
///         Written now, ready to run unchanged the moment that lands.
///     </para>
/// </summary>
public class EliteDungeonTicketUseItemHandlerTests
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
                throw new TimeoutException("EliteDungeonTicketUseItemHandler task never completed.");
        }

        return await task;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        EliteDungeonTicketUseItemHandler Handler) SetUp()
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
        var handler = new EliteDungeonTicketUseItemHandler(writer, eventLog,
            NullLogger<EliteDungeonTicketUseItemHandler>.Instance);

        return (zone, state!, characters, handler);
    }

    private static ItemStack Ticket(int itemId, int quantity = 1)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, int itemId, ItemStack item)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, 0, 0, item,
            new ItemDefinition(WorldDataTestRows.Item(itemId), []), 0);
    }

    [Theory]
    [InlineData(1047, 180)]
    [InlineData(1097, 120)]
    [InlineData(1098, 60)]
    public async Task Use_AddsTheDocumentedAmount_ConsumesOneUnit(int itemId, int expectedAmount)
    {
        var (zone, state, characters, handler) = SetUp();
        state.EliteDungeonTime = 10;
        var item = Ticket(itemId);

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, itemId, item), CancellationToken.None), zone);

        Assert.Equal(0, response.Result);
        Assert.Equal(10 + expectedAmount, response.Value);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(10 + expectedAmount, after!.EliteDungeonTime);
        Assert.NotNull(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task WouldExceedCeiling_FailsCleanly_NoConsumption()
    {
        var (zone, state, characters, handler) = SetUp();
        state.EliteDungeonTime = BankedCounterMath.GlobalCeiling;
        var item = Ticket(1047);

        var response = await handler.HandleAsync(Context(zone, state, 1047, item), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Equal(BankedCounterMath.GlobalCeiling, state.EliteDungeonTime);
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task ZeroStackQuantity_FailsCleanly()
    {
        var (zone, state, characters, handler) = SetUp();
        var item = Ticket(1047, quantity: 0);

        var response = await handler.HandleAsync(Context(zone, state, 1047, item), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(characters.LastReplacedContainer);
    }
}
