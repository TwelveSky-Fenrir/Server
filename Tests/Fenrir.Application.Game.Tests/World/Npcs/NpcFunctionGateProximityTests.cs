using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Npcs;

public class NpcFunctionGateProximityTests
{
    private static ZoneDefinition ZoneWith(params (int NpcId, float X, float Y, float Z)[] spawns)
    {
        var rows = spawns
            .Select((s, i) => new ZoneNpcSpawnRowDto(1, (short)i, s.NpcId, s.X, s.Y, s.Z, 0f))
            .ToImmutableArray();
        return new ZoneDefinition(WorldDataTestRows.Zone(1), [], [], rows, [], []);
    }

    [Fact]
    public void NamedNpcWithinRadius_IsNear()
    {
        var zone = ZoneWith((NpcId: 777, X: 0, Y: 0, Z: 0));

        Assert.Equal(NpcProximity.Near, NpcFunctionGate.CheckNpcProximity(zone, 777, 0, 0, 50));
    }

    [Fact]
    public void NamedNpcBeyondRadius_IsFar_NotNotInZone()
    {
        var zone = ZoneWith((NpcId: 777, X: 0, Y: 0, Z: 0));

        Assert.Equal(NpcProximity.Far, NpcFunctionGate.CheckNpcProximity(zone, 777, 100.1f, 0, 0));
    }

    [Fact]
    public void NamedNpcExactlyAtRadius_IsFar_SquaredCompareIsStrictlyLessThan()
    {
        var zone = ZoneWith((NpcId: 777, X: 0, Y: 0, Z: 0));

        Assert.Equal(NpcProximity.Far, NpcFunctionGate.CheckNpcProximity(zone, 777, 100f, 0, 0));
    }

    [Fact]
    public void NamedNpcCountsAllThreeAxes()
    {
        var zone = ZoneWith((NpcId: 777, X: 0, Y: 0, Z: 0));

        Assert.Equal(NpcProximity.Far, NpcFunctionGate.CheckNpcProximity(zone, 777, 60, 60, 60));
    }

    [Fact]
    public void DifferentNpcNumberAdjacent_ButRequestedNpcAbsent_IsNotInZone()
    {
        var zone = ZoneWith((NpcId: 10, X: 0, Y: 0, Z: 0));

        Assert.Equal(NpcProximity.NpcNotInZone, NpcFunctionGate.CheckNpcProximity(zone, 777, 0, 0, 0));
    }

    [Fact]
    public void EmptyZone_IsNotInZone()
    {
        Assert.Equal(NpcProximity.NpcNotInZone, NpcFunctionGate.CheckNpcProximity(ZoneWith(), 777, 0, 0, 0));
    }

    [Fact]
    public void SecondPlacementOfSameNpc_MakesItNear_WhenFirstIsOutOfRange()
    {
        var zone = ZoneWith((NpcId: 777, X: 500, Y: 0, Z: 500), (NpcId: 777, X: 0, Y: 0, Z: 0));

        Assert.Equal(NpcProximity.Near, NpcFunctionGate.CheckNpcProximity(zone, 777, 0, 0, 0));
    }

    [Fact]
    public void AllPlacementsOfSameNpcOutOfRange_IsFar()
    {
        var zone = ZoneWith((NpcId: 777, X: 500, Y: 0, Z: 500), (NpcId: 777, X: 400, Y: 0, Z: 0));

        Assert.Equal(NpcProximity.Far, NpcFunctionGate.CheckNpcProximity(zone, 777, 0, 0, 0));
    }
}
