using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneWeaponUnequipEffectClearTests
{
    private const byte WeaponSlot = EquipmentSlots.WeaponSlot;
    private const byte ArmorSlot = 0;
    private const int ActiveBuffSlot = 5;

    private static (Zone Zone, PlayerRuntimeState State) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!);
    }

    private static void SeedEquipment(Zone zone, ImmutableDictionary<byte, ItemStack> equipment)
    {
        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(ContainerMatrix.Equipment, equipment));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static void ArmActiveEffects(PlayerRuntimeState state)
    {
        state.Buffs.Buff[ActiveBuffSlot * 2] = 100;
        state.Buffs.Buff[ActiveBuffSlot * 2 + 1] = 1;
        state.NoManaCount = 7;
        state.PartyBuffAct = PartyBuffAction.Cast;
        state.AutoHuntConfig = new AutoHunt
        {
            BuffType = 1,
            BuffStore = [11, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            HuntType = 1,
            AttackType = [0, 0, 0, 0],
            MonNum = 0,
            ItemType = 0,
            InvenCmd = 0,
            DeathCmd = 0,
            AnimalPreyCmd = 0,
            AnimalFoodCmd = 0
        };
    }

    [Fact]
    public void UnequippingWeaponSlot_ClearsAllEffectsAndResetCounters()
    {
        var (zone, state) = SetUp();
        SeedEquipment(zone, ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(WeaponSlot, new ItemStack(9001, 1, 0, 0, 0, 0, 0, 0, 0, 12345, 1))
            .SetItem(ArmorSlot, new ItemStack(9002, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2)));
        ArmActiveEffects(state);

        SeedEquipment(zone, ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(ArmorSlot, new ItemStack(9002, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2)));

        Assert.Equal(0, state.Buffs.Buff[ActiveBuffSlot * 2]);
        Assert.Equal(0, state.Buffs.Buff[ActiveBuffSlot * 2 + 1]);
        Assert.Equal(0, state.NoManaCount);
        Assert.Equal(PartyBuffAction.None, state.PartyBuffAct);
        Assert.NotNull(state.AutoHuntConfig);
        Assert.All(state.AutoHuntConfig!.Value.BuffStore, value => Assert.Equal(0, value));
    }

    [Fact]
    public void UnequippingNonWeaponSlot_LeavesEffectsUntouched()
    {
        var (zone, state) = SetUp();
        SeedEquipment(zone, ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(WeaponSlot, new ItemStack(9001, 1, 0, 0, 0, 0, 0, 0, 0, 12345, 1))
            .SetItem(ArmorSlot, new ItemStack(9002, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2)));
        ArmActiveEffects(state);

        SeedEquipment(zone, ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(WeaponSlot, new ItemStack(9001, 1, 0, 0, 0, 0, 0, 0, 0, 12345, 1)));

        Assert.Equal(1, state.Buffs.Buff[ActiveBuffSlot * 2 + 1]);
        Assert.Equal(7, state.NoManaCount);
        Assert.Equal(PartyBuffAction.Cast, state.PartyBuffAct);
        Assert.Equal(11, state.AutoHuntConfig!.Value.BuffStore[0]);
    }

    [Fact]
    public void EquippingIntoPreviouslyEmptyWeaponSlot_DoesNotTriggerClear()
    {
        var (zone, state) = SetUp();
        ArmActiveEffects(state);

        SeedEquipment(zone, ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(WeaponSlot, new ItemStack(9001, 1, 0, 0, 0, 0, 0, 0, 0, 12345, 1)));

        Assert.Equal(1, state.Buffs.Buff[ActiveBuffSlot * 2 + 1]);
        Assert.Equal(7, state.NoManaCount);
        Assert.Equal(PartyBuffAction.Cast, state.PartyBuffAct);
    }
}
