using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Tests.Simulation;

public class PersonalShopRegionEnforcementSystemTests
{
    private static Zone SetUp(short mapId)
    {
        return ZoneTestKit.CreateZone(mapId, simulationSystems: [new PersonalShopRegionEnforcementSystem()]);
    }

    private static (ZoneClientSession Session, PlayerRuntimeState State) EnterPlayer(Zone zone, int characterId,
        byte tribe, float x, float y, float z)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, zone.MapId, tribe: tribe, posX: x, posY: y, posZ: z)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return (session, state!);
    }

    [Fact]
    public void ShopClosed_OutsideEveryRegion_NeverDisconnected()
    {
        var zone = SetUp(37);
        var (session, state) = EnterPlayer(zone, 1, 0, 999_999f, 0f, 999_999f);
        state.PshopOpen = false;

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public void ShopOpen_InsidePermittedRegion_NeverDisconnected()
    {
        var zone = SetUp(37);
        var (session, state) = EnterPlayer(zone, 1, 0, 1f, 0f, -1478f);
        state.PshopOpen = true;

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.Null(session.DisconnectReason);
        Assert.True(state.PshopOpen);
    }

    [Fact]
    public void ShopOpen_DriftedOutsideEveryRegion_DisconnectedWithStateViolation()
    {
        var zone = SetUp(37);
        var (session, state) = EnterPlayer(zone, 1, 0, 1f, 0f, -1478f);
        state.PshopOpen = true;

        state.PosX = 999_999f;

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public void ShopOpen_OnAMapNoneOfTheFiveRegionsRecognize_AlwaysDisconnected()
    {
        var zone = SetUp(2);
        var (session, state) = EnterPlayer(zone, 1, 0, 4f, 0f, -2f);
        state.PshopOpen = true;

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public void ShopOpen_MidZoneTransfer_SkippedForThatTick()
    {
        var zone = SetUp(37);
        var (session, state) = EnterPlayer(zone, 1, 0, 1f, 0f, -1478f);
        state.PshopOpen = true;
        state.IsMovingZone = true;
        state.PosX = 999_999f;

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.Null(session.DisconnectReason);
    }
}
