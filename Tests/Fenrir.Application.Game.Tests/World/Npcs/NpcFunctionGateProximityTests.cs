using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Npcs;

/// <summary>
///     Covers the NPC-id-keyed proximity primitive <see cref="NpcFunctionGate.CheckNpcProximity" /> (the
///     per-NPC-coordinate <c>ReturnZoneCoord</c> variant used by the quest-anchor gate), distinct from the
///     menu-function-keyed <see cref="NpcFunctionGate.IsAvailable" /> covered by <c>NpcFunctionGateTests</c>.
/// </summary>
public class NpcFunctionGateProximityTests
{
    private static ZoneDefinition ZoneWith(params (int NpcId, float X, float Y, float Z)[] spawns)
    {
        var rows = spawns
            .Select((s, i) => new ZoneNpcSpawnRowDto(1, (short)i, s.NpcId, s.X, s.Y, s.Z, 0f))
            .ToImmutableArray();
        return new ZoneDefinition(WorldDataTestRows.Zone(1), [], [], rows, []);
    }

    [Fact]
    public void NamedNpcWithinRadius_IsNear()
    {
        var zone = ZoneWith((NpcId: 777, X: 0, Y: 0, Z: 0));

        // 50 units away on Z -- inside the sqrt(10000)=100 radius.
        Assert.Equal(NpcProximity.Near, NpcFunctionGate.CheckNpcProximity(zone, 777, 0, 0, 50));
    }

    [Fact]
    public void NamedNpcBeyondRadius_IsFar_NotNotInZone()
    {
        var zone = ZoneWith((NpcId: 777, X: 0, Y: 0, Z: 0));

        // 100.1 units away -- just past the radius; the NPC IS placed, so this is Far, not NpcNotInZone.
        Assert.Equal(NpcProximity.Far, NpcFunctionGate.CheckNpcProximity(zone, 777, 100.1f, 0, 0));
    }

    [Fact]
    public void NamedNpcExactlyAtRadius_IsFar_SquaredCompareIsStrictlyLessThan()
    {
        var zone = ZoneWith((NpcId: 777, X: 0, Y: 0, Z: 0));

        // Distance exactly 100 -> squared 10000, and the compare is `< 10000`, so the boundary is excluded.
        Assert.Equal(NpcProximity.Far, NpcFunctionGate.CheckNpcProximity(zone, 777, 100f, 0, 0));
    }

    [Fact]
    public void NamedNpcCountsAllThreeAxes()
    {
        var zone = ZoneWith((NpcId: 777, X: 0, Y: 0, Z: 0));

        // 60/60/60 -> squared 10800 > 10000 -> Far. Confirms the vertical (Y) axis is included in the distance.
        Assert.Equal(NpcProximity.Far, NpcFunctionGate.CheckNpcProximity(zone, 777, 60, 60, 60));
    }

    [Fact]
    public void DifferentNpcNumberAdjacent_ButRequestedNpcAbsent_IsNotInZone()
    {
        // NPC 10 is right on top of the player, but the caller asked about NPC 777, which is not placed at all.
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
        // Two placements of NPC 777: one far, one adjacent. Any placement within radius reports Near.
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
