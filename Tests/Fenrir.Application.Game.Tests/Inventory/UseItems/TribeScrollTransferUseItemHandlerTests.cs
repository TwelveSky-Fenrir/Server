using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

/// <summary>
///     Drives <see cref="TribeScrollTransferUseItemHandler" /> (op23 items 8153/8154) directly, over a real
///     <see cref="Zone" /> so its <c>PostTribeProgressCommandAndWaitAsync</c>/<c>PostInventoryCommandAndWaitAsync</c>
///     mirrors actually apply. Covers all 13 gates in <see cref="TribeScrollTransferGate" />, the two genuine
///     disconnects, the atomic conversion call, and the best-effort equip/skill remap.
/// </summary>
public class TribeScrollTransferUseItemHandlerTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short HomeZoneMapId = 37;
    private const byte HomeShardId = 1;
    private const short EligibleLevel = 145;

    // Synthetic equivalence catalog: item group 0 (tribe0=2000, tribe1=2001, tribe2=2002), skill group 0
    // (tribe0=1000, tribe1=1001, tribe2=1002) -- same shape as TribeConversionResolverTests' own fixture.
    private static TribeConversionResolver BuildResolver()
    {
        var skills = new[]
        {
            new TribeSkillEquivalenceRowDto(0, 0, 1000),
            new TribeSkillEquivalenceRowDto(0, 1, 1001),
            new TribeSkillEquivalenceRowDto(0, 2, 1002)
        };

        var items = new[]
        {
            new TribeItemEquivalenceRowDto(0, 0, 2000),
            new TribeItemEquivalenceRowDto(0, 1, 2001),
            new TribeItemEquivalenceRowDto(0, 2, 2002)
        };

        var costumes = Array.Empty<TribeCostumeEquivalenceRowDto>();

        return new TribeConversionResolver(skills, items, costumes);
    }

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
                throw new TimeoutException("TribeScrollTransferUseItemHandler task never completed.");
        }

        return await task;
    }

    private static (Zone Zone, ZoneClientSession Session, PlayerRuntimeState State,
        FakeTribeConversionRepository TribeConversion, PartyRegistry Parties,
        TribeScrollTransferUseItemHandler Handler) SetUp(bool homeZoneOnline = true,
        short configuredHomeMapId = HomeZoneMapId, short zoneMapId = 1)
    {
        var parties = new PartyRegistry();
        var zone = ZoneTestKit.CreateZone(zoneMapId, partyRegistry: parties,
            worldData: ZoneTestKit.EmptyWorldData());
        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, zoneMapId, tribe: 0,
            level: EligibleLevel)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        state!.PreviousTribe = 0;

        var tribeConversion = new FakeTribeConversionRepository();

        IGameServerDirectoryRepository directory = homeZoneOnline
            ? new FakeGameServerDirectoryRepository(
                new ShardDirectoryEntryDto(HomeShardId, "localhost", 11000, 0, 100, 0f))
            : new FakeGameServerDirectoryRepository();

        var hostedMaps = homeZoneOnline
            ? new Dictionary<byte, short[]> { [HomeShardId] = [HomeZoneMapId] }
            : new Dictionary<byte, short[]>();
        IShardMapAssignmentRepository shardMapAssignments = new FakeShardMapAssignmentRepository(hostedMaps);

        var options = new GameServerOptions { FactionTransferHomeZoneMapId = configuredHomeMapId };
        var handler = new TribeScrollTransferUseItemHandler(tribeConversion, BuildResolver(), parties, directory,
            shardMapAssignments, ZoneTestKit.EmptyWorldData(), options,
            NullLogger<TribeScrollTransferUseItemHandler>.Instance);

        return (zone, session, state, tribeConversion, parties, handler);
    }

    private static ItemStack Scroll(int itemId = 8153, int quantity = 1)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    private static ItemDefinition Definition(int itemId = 8153)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId), []);
    }

    private static UseItemContext Context(Zone zone, PlayerRuntimeState state, byte page, byte index,
        int value, int itemId = 8153)
    {
        return new UseItemContext(zone, state, CharacterId, AccountId, page, index, Scroll(itemId), Definition(itemId),
            value);
    }

    [Fact]
    public async Task Eligible_AppliesConversion_ConsumesScroll_RemapsEquipmentAndSkills_SendsReturnToAutoZone()
    {
        var (zone, session, state, tribeConversion, _, handler) = SetUp();
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Scroll()));
        state.Inventory.ReplaceContainer(ContainerMatrix.Equipment,
            ImmutableDictionary<byte, ItemStack>.Empty
                .SetItem(2, new ItemStack(2000, 1, 5, 3, 0, 0, 0, 0, 0, 0, 1)));
        state.LearnedSkills = state.LearnedSkills.SetItem(0, new LearnedSkill(1000, 7));

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.Null(session.DisconnectReason);

        // Atomic conversion call reached the repository with the projected (scroll-removed) container.
        Assert.NotNull(tribeConversion.LastCall);
        var call = tribeConversion.LastCall!.Value;
        Assert.Equal(CharacterId, call.CharacterId);
        Assert.Equal(8153, call.ItemId);
        Assert.Equal((byte)1, call.ToTribe);
        Assert.Equal(ContainerMatrix.InventoryPage0, call.Container);
        Assert.DoesNotContain(call.Items, i => i.Slot == 0);

        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(1, after!.Tribe);
        Assert.Equal(1, after.PreviousTribe);

        // Scroll consumed from live state.
        Assert.Null(after.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));

        // Equipment remapped 2000 (tribe0) -> 2001 (tribe1), enchant/combine preserved.
        var equip = after.Inventory.GetSlot(ContainerMatrix.Equipment, 2);
        Assert.NotNull(equip);
        Assert.Equal(2001, equip!.Value.ItemId);
        Assert.Equal(5, equip.Value.Enchant);
        Assert.Equal(3, equip.Value.Combine);

        // Skill remapped 1000 (tribe0) -> 1001 (tribe1).
        Assert.Equal(1001, after.LearnedSkills[0].SkillId);
        Assert.Equal(7, after.LearnedSkills[0].Grade);
    }

    [Fact]
    public async Task StackedQuantityGreaterThanOne_StillConsumesTheWholeSlot()
    {
        var (zone, _, state, _, _, handler) = SetUp();
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Scroll(quantity: 5)));

        var response = await RunToCompletionAsync(
            handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1), CancellationToken.None),
            zone);

        Assert.Equal(0, response.Result);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Null(after!.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(255)]
    public async Task InvalidDestinationTribe_Disconnects_NoResponseSent(int wireValue)
    {
        var (zone, session, state, tribeConversion, _, handler) = SetUp();

        await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, wireValue),
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task DestinationEqualsPreviousTribe_Disconnects()
    {
        var (zone, session, state, tribeConversion, _, handler) = SetUp();
        state.PreviousTribe = 0;

        await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 0), CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task BelowLevel145_FailsCleanly()
    {
        var (zone, session, state, tribeConversion, _, handler) = SetUp();
        state.Level = 144;

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(session.DisconnectReason);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task HomeZoneNotHostedByAnyLiveShard_FailsCleanly()
    {
        var (zone, _, state, tribeConversion, _, handler) = SetUp(homeZoneOnline: false);

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task UnconfiguredHomeMapId_DefaultsToPermanentlyOffline_FailsCleanly()
    {
        var (zone, _, state, tribeConversion, _, handler) = SetUp(configuredHomeMapId: 0);

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task NotStandingInOwnTribesCapital_FailsCleanly()
    {
        // Zone 1 is tribe 0's own capital (IsValidTown); zone 6 is tribe 1's -- standing in the WRONG one.
        var (zone, _, state, tribeConversion, _, handler) = SetUp(zoneMapId: 6);

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public async Task HoldsAnyTribeRoleTier_FailsCleanly(byte tribeRole)
    {
        var (zone, _, state, tribeConversion, _, handler) = SetUp();
        state.TribeRole = tribeRole;

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task IsRegisteredAsMentor_FailsCleanly()
    {
        var (zone, _, state, tribeConversion, _, handler) = SetUp();
        state.StudentCharacterId = 999;

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task IsRegisteredAsMentee_FailsCleanly()
    {
        var (zone, _, state, tribeConversion, _, handler) = SetUp();
        state.TeacherCharacterId = 999;

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task InParty_FailsCleanly()
    {
        var (zone, _, state, tribeConversion, parties, handler) = SetUp();
        Assert.Equal(PartyInviteOutcome.Sent,
            parties.TryInvite(999, EligibleLevel, 0, CharacterId, EligibleLevel, 0));
        Assert.True(parties.TryAnswer(CharacterId, true, out _, out _));
        Assert.True(parties.IsInParty(CharacterId));

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task HasGuild_FailsCleanly()
    {
        var (zone, _, state, tribeConversion, _, handler) = SetUp();
        state.GuildId = 5;

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task CapeEquipped_FailsCleanly()
    {
        var (zone, _, state, tribeConversion, _, handler) = SetUp();
        state.Inventory.ReplaceContainer(ContainerMatrix.Equipment,
            ImmutableDictionary<byte, ItemStack>.Empty
                .SetItem((byte)SkillGradeAuthority.CapeSlotIndex, new ItemStack(9999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1)));

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }

    [Fact]
    public async Task HasRegisteredFriend_FailsCleanly()
    {
        var (zone, _, state, tribeConversion, _, handler) = SetUp();
        state.Friends[0] = 999;

        var response = await handler.HandleAsync(Context(zone, state, ContainerMatrix.InventoryPage0, 0, 1),
            CancellationToken.None);

        Assert.Equal(1, response.Result);
        Assert.Null(tribeConversion.LastCall);
    }
}
