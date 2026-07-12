using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class UseHotkeyItemServicePotionTests
{
    private const int PotionItemId = 2001;
    private const byte ConsumableSort = HotkeyItemConsumptionResolver.ConsumableItemCategory;
    private const int LifeStatSort = 10;
    private const int ManaStatSort = 11;

    private static byte[] ExpectedFrames(params ReadOnlySpan<AvatarStatUpdateResponse> packets)
    {
        var frameSize = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();
        var buffer = new byte[frameSize * packets.Length];
        for (var i = 0; i < packets.Length; i++)
            FrameWriter.WriteFrame(in packets[i], buffer.AsSpan(i * frameSize, frameSize));
        return buffer;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe, FakeCharacterRepository Characters,
        UseHotkeyItemService Service) SetUp(
            int potionType1, int potionType2, int life, int maxLife, int mana, int maxMana, int quantity = 1)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [PotionItemId] = new(
                WorldDataTestRows.Item(PotionItemId) with
                {
                    Sort = ConsumableSort, PotionType1 = (short)potionType1, PotionType2 = (short)potionType2
                },
                [])
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(itemsById);

        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.SetHotkeySlot(0, 0, new HotkeySlot(HotkeyBindingKind.Item, PotionItemId, quantity));
        state.Life = life;
        state.MaxLife = maxLife;
        state.Mana = mana;
        state.MaxMana = maxMana;

        var characters = new FakeCharacterRepository();
        var service = new UseHotkeyItemService(characters, worldData, NullLogger<UseHotkeyItemService>.Instance);
        return (zone, state, pipe, characters, service);
    }

    [Fact]
    public async Task FlatLifePotion_PotionType1_GainClampedToRemainingHeadroom()
    {
        var (zone, state, pipe, _, service) = SetUp(potionType1: 1, potionType2: 100, life: 800, maxLife: 840,
            mana: 300, maxMana: 320);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(840, state.Life);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(new AvatarStatUpdateResponse { Sort = LifeStatSort, Value = 840, Value2 = 0 });
        Assert.Equal(expected, sent);
    }

    [Fact]
    public async Task PercentManaPotion_PotionType4_GainIsMaxTimesPercentTruncatedTowardZero()
    {
        var (zone, state, pipe, _, service) = SetUp(potionType1: 4, potionType2: 33, life: 800, maxLife: 840,
            mana: 100, maxMana: 320);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(205, state.Mana);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(new AvatarStatUpdateResponse { Sort = ManaStatSort, Value = 205, Value2 = 0 });
        Assert.Equal(expected, sent);
    }

    [Fact]
    public async Task LifeAndManaPotion_PotionType5_LifeAlreadyFull_OnlySendsManaNotification()
    {
        var (zone, state, pipe, _, service) = SetUp(potionType1: 5, potionType2: 50, life: 840, maxLife: 840,
            mana: 100, maxMana: 320);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(840, state.Life);
        Assert.Equal(260, state.Mana);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(new AvatarStatUpdateResponse { Sort = ManaStatSort, Value = 260, Value2 = 0 });
        Assert.Equal(expected, sent);
    }

    [Fact]
    public async Task LifeAndManaPotion_PotionType5_BothAlreadyFull_RejectedCleanly_NoNotifications()
    {
        var (zone, state, pipe, characters, service) = SetUp(potionType1: 5, potionType2: 50, life: 840,
            maxLife: 840, mana: 320, maxMana: 320);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Equal(840, state.Life);
        Assert.Equal(320, state.Mana);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task FlatLifePotion_AlreadyAtMaxLife_RejectedCleanly_SlotUntouched()
    {
        var (zone, state, pipe, characters, service) = SetUp(potionType1: 1, potionType2: 100, life: 840,
            maxLife: 840, mana: 300, maxMana: 320, quantity: 3);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Equal(840, state.Life);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Equal(3, state.GetHotkeySlot(0, 0).Value2);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task FlatLifePotion_CannotUseConsumables_RejectedCleanly()
    {
        var (zone, state, pipe, characters, service) = SetUp(potionType1: 1, potionType2: 100, life: 800,
            maxLife: 840, mana: 300, maxMana: 320);
        state.CanUseConsumables = false;

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Equal(800, state.Life);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task FlatLifePotion_LastUnit_ClearsTheSlotInsteadOfDecrementing()
    {
        var (zone, state, pipe, characters, service) = SetUp(potionType1: 1, potionType2: 10, life: 800,
            maxLife: 840, mana: 300, maxMana: 320, quantity: 1);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.True(state.GetHotkeySlot(0, 0).IsEmpty);

        var upsert = Assert.NotNull(characters.LastUpsertHotkeySlot);
        Assert.Equal((int)HotkeyBindingKind.None, upsert.Value2);
        ZoneTestKit.DrainOutbound(pipe);
    }
}
