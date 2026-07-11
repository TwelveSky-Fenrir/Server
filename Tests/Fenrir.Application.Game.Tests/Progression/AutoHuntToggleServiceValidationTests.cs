using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Progression;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Progression;

public class AutoHuntToggleServiceValidationTests
{
    private static AutoHunt ValidBlob()
    {
        return new AutoHunt
        {
            BuffType = 0, BuffStore = new int[16], HuntType = 0, AttackType = new int[4],
            MonNum = 0, ItemType = 0, InvenCmd = 0, DeathCmd = 0, AnimalPreyCmd = 0, AnimalFoodCmd = 0
        };
    }

    private static AutoHunt ValidBlobWithAttackSkill()
    {
        return ValidBlob() with { AttackType = [15, 6, 0, 0] };
    }

    private static AutoHunt MalformedBlob()
    {
        return ValidBlob() with { BuffStore = [-1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] };
    }

    private static (AutoHuntToggleService Service, Zone Zone, PlayerRuntimeState State) SetUp(short mapId,
        bool equipWeapon = false)
    {
        var service = new AutoHuntToggleService(new FakeCharacterRepository(),
            NullLogger<AutoHuntToggleService>.Instance);
        var zone = ZoneTestKit.CreateZone(mapId);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        if (equipWeapon)
        {
            var equipment = ImmutableArray.Create(new InventoryContainerSnapshot(ContainerMatrix.Equipment,
                ImmutableDictionary<byte, ItemStack>.Empty.SetItem(7,
                    new ItemStack(9001, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0))));
            zone.PostInventoryCommand(new InventoryZoneCommand(10, equipment, null));
            zone.Tick(TimeSpan.FromMilliseconds(50));
        }

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (service, zone, state!);
    }

    [Fact]
    public async Task MalformedBlobOnEnable_IsRejected_AndNeverStored()
    {
        var (service, zone, state) = SetUp(1);
        var request = new AutoHuntToggleRequest { Sort = 1, AutoHunt = MalformedBlob() };

        var result = await service.ToggleAsync(10, zone, state, request, CancellationToken.None);

        Assert.True(result.Aborted);
        Assert.False(state.AutoHuntEnabled);
        Assert.Null(state.AutoHuntConfig);
    }

    [Fact]
    public async Task MalformedBlobOnDisable_IsAlsoRejected()
    {
        var (service, zone, state) = SetUp(1);
        var request = new AutoHuntToggleRequest { Sort = 0, AutoHunt = MalformedBlob() };

        var result = await service.ToggleAsync(10, zone, state, request, CancellationToken.None);

        Assert.True(result.Aborted);
    }

    [Fact]
    public async Task ValidBlobEnableOnBlockedZone_IsRejectedByTheEnableGate()
    {
        var (service, zone, state) = SetUp(38, true);
        var request = new AutoHuntToggleRequest { Sort = 1, AutoHunt = ValidBlobWithAttackSkill() };

        var result = await service.ToggleAsync(10, zone, state, request, CancellationToken.None);

        Assert.True(result.Aborted);
        Assert.False(state.AutoHuntEnabled);
    }
}
