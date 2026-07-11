using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

/// <summary>
///     Drives <see cref="ForcedNeutralTribeResetUseItemHandler" /> (op23 item 8100) directly -- not through
///     <see cref="UseItemHandlerRegistry" />, since wiring the handler into that registry's constructor is a
///     verbatim edit to an existing file reported separately (see this workstream's wiringManifest), not
///     something a new-files-only pass can apply itself. Every precondition, the tribe flip, the item
///     consumption, and the neutral-home-zone reachability resolution are exercised here regardless.
/// </summary>
public class ForcedNeutralTribeResetUseItemHandlerTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short HomeMapId = 140;
    private const byte HomeShardId = 1;

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
                throw new TimeoutException("ForcedNeutralTribeResetUseItemHandler task never completed.");
        }

        return await task;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        ForcedNeutralTribeResetUseItemHandler Handler) SetUp(bool neutralHomeZoneOnline = true,
        short configuredHomeMapId = HomeMapId)
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
        var writer = new UseItemInventoryWriter(characters, NullLogger<UseItemInventoryWriter>.Instance);

        IGameServerDirectoryRepository directory = neutralHomeZoneOnline
            ? new FakeGameServerDirectoryRepository(
                new ShardDirectoryEntryDto(HomeShardId, "localhost", 11000, 0, 100, 0f))
            : new FakeGameServerDirectoryRepository();

        var hostedMaps = neutralHomeZoneOnline
            ? new Dictionary<byte, short[]> { [HomeShardId] = [HomeMapId] }
            : new Dictionary<byte, short[]>();
        IShardMapAssignmentRepository shardMapAssignments = new FakeShardMapAssignmentRepository(hostedMaps);

        var options = new GameServerOptions { ForcedNeutralResetHomeMapId = configuredHomeMapId };
        var handler = new ForcedNeutralTribeResetUseItemHandler(characters, writer, directory, shardMapAssignments,
            options, NullLogger<ForcedNeutralTribeResetUseItemHandler>.Instance);

        return (zone, state!, characters, handler);
    }

    private static ItemStack Ticket(int quantity = 1)
    {
        return new ItemStack(ForcedNeutralTribeResetUseItemHandler.ItemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    private static ItemDefinition Definition()
    {
        return new ItemDefinition(WorldDataTestRows.Item(ForcedNeutralTribeResetUseItemHandler.ItemId), []);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, 0, 0, Ticket(), Definition(), 0);
    }

    [Fact]
    public async Task Eligible_ResetsTribeToNeutral_ConsumesTheItem_AndPersistsViaTheTribeFourConversionProcedure()
    {
        var (zone, state, characters, handler) = SetUp();
        state.Tribe = 0;
        state.Level = 113;
        state.TribeRole = 0;
        state.QuestStepPermanent = 4;
        state.QuestActiveFlag = 1;
        state.QuestSort = 2;
        state.QuestTargetPhase = 3;
        state.QuestKillCounter = 5;

        var response = await RunToCompletionAsync(handler.HandleAsync(Context(zone, state), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(3, after!.Tribe);

        // The item was consumed.
        Assert.NotNull(characters.LastReplacedContainer);

        // Durable write reused the existing atomic Tribe+quest-state procedure, with the character's own
        // current quest-progress fields written back UNCHANGED (never touched by this action).
        Assert.Single(characters.TribeFourConversions);
        var (writtenCharacterId, newTribe, stepPermanent, activeQuestId, qSort, targetPhase, killCounter) =
            characters.TribeFourConversions[0];
        Assert.Equal(CharacterId, writtenCharacterId);
        Assert.Equal((byte)3, newTribe);
        Assert.Equal(4, stepPermanent);
        Assert.Equal(1, activeQuestId);
        Assert.Equal(2, qSort);
        Assert.Equal(3, targetPhase);
        Assert.Equal(5, killCounter);
    }

    [Fact]
    public async Task BelowLevel113_FailsCleanly_NoItemConsumed_NoTribeWrite()
    {
        var (zone, state, characters, handler) = SetUp();
        state.Tribe = 0;
        state.Level = 112;

        var response = await handler.HandleAsync(Context(zone, state), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Equal(0, state.Tribe);
        Assert.Null(characters.LastReplacedContainer);
        Assert.Empty(characters.TribeFourConversions);
    }

    [Fact]
    public async Task AlreadyNeutral_FailsCleanly()
    {
        var (zone, state, characters, handler) = SetUp();
        state.Tribe = 3;
        state.Level = 120;

        var response = await handler.HandleAsync(Context(zone, state), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Empty(characters.TribeFourConversions);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public async Task HoldsAnyTribeRoleTier_FailsCleanly(byte tribeRole)
    {
        var (zone, state, characters, handler) = SetUp();
        state.Tribe = 0;
        state.Level = 120;
        state.TribeRole = tribeRole;

        var response = await handler.HandleAsync(Context(zone, state), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Empty(characters.TribeFourConversions);
    }

    [Fact]
    public async Task HasGuild_FailsCleanly()
    {
        var (zone, state, characters, handler) = SetUp();
        state.Tribe = 0;
        state.Level = 120;
        state.GuildId = 5;

        var response = await handler.HandleAsync(Context(zone, state), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Empty(characters.TribeFourConversions);
    }

    [Fact]
    public async Task HasRegisteredFriend_FailsCleanly()
    {
        var (zone, state, characters, handler) = SetUp();
        state.Tribe = 0;
        state.Level = 120;
        state.Friends[0] = 999;

        var response = await handler.HandleAsync(Context(zone, state), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Empty(characters.TribeFourConversions);
    }

    [Fact]
    public async Task NeutralHomeZoneNotHostedByAnyLiveShard_FailsCleanly_EvenWhenEveryOtherPreconditionPasses()
    {
        var (zone, state, characters, handler) = SetUp(neutralHomeZoneOnline: false);
        state.Tribe = 0;
        state.Level = 120;

        var response = await handler.HandleAsync(Context(zone, state), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Empty(characters.TribeFourConversions);
    }

    [Fact]
    public async Task UnconfiguredHomeMapId_DefaultsToPermanentlyOffline_FailsCleanly()
    {
        // ForcedNeutralResetHomeMapId <= 0 (the shipped default) must short-circuit to "offline" without even
        // consulting the directory -- see ForcedNeutralTribeResetUseItemHandler.IsNeutralHomeZoneOnlineAsync's
        // own remarks on why this stays fail-safe until an operator confirms + configures the real map id.
        var (zone, state, characters, handler) = SetUp(configuredHomeMapId: 0);
        state.Tribe = 0;
        state.Level = 120;

        var response = await handler.HandleAsync(Context(zone, state), CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Empty(characters.TribeFourConversions);
    }

    [Fact]
    public async Task PreviousTribeAndOtherWardrobeFieldsAreNeverTouched()
    {
        var (zone, state, characters, handler) = SetUp();
        state.Tribe = 0;
        state.PreviousTribe = 1;
        state.Level = 120;

        var response = await RunToCompletionAsync(handler.HandleAsync(Context(zone, state), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(3, after!.Tribe);
        // Previous tribe (race/starter-kit template) is a pure faction flip -- never remapped by this action.
        Assert.Equal(1, after.PreviousTribe);
    }
}
