using Fenrir.Application.Game.Domain.World.Geometry;

namespace Fenrir.Application.Game.Tests.World.Geometry;

/// <summary>
///     Covers <see cref="ZoneCanonicalGeometryMap.ResolveCanonicalMapId" /> against the legacy
///     <c>WORLD_FOR_GXD::LoadWM</c> remap switch (<c>Server/ts25zone/S09_MyWorld.cpp:96-368</c>): each asserted
///     pair is a physical map id and the canonical <c>.WM</c> id its own <c>case</c> label falls through to.
/// </summary>
public class ZoneCanonicalGeometryMapTests
{
    [Theory]
    // Labyrinth family -> 175 (:98-138), sampling each contiguous sub-range plus the low G-instances.
    [InlineData(176, 175)]
    [InlineData(193, 175)]
    [InlineData(19, 175)]
    [InlineData(34, 175)]
    [InlineData(36, 175)]
    // Cave/ruin/tomb triples (:140-159).
    [InlineData(22, 16)]
    [InlineData(28, 16)]
    [InlineData(23, 17)]
    [InlineData(30, 18)]
    // ND/RS/GT instance triples (:161-215).
    [InlineData(42, 40)]
    [InlineData(45, 43)]
    [InlineData(48, 46)]
    [InlineData(68, 62)]
    [InlineData(70, 64)]
    // Four-way instance blocks (:217-239).
    [InlineData(79, 76)]
    [InlineData(83, 80)]
    [InlineData(167, 101)]
    // Big 104 block incl. the 251-266 Odawa range (:241-275).
    [InlineData(105, 104)]
    [InlineData(117, 104)]
    [InlineData(251, 104)]
    [InlineData(266, 104)]
    // 126 block incl. the LNW33 39/144/145/313 branch (:277-313).
    [InlineData(127, 126)]
    [InlineData(221, 126)]
    [InlineData(39, 126)]
    [InlineData(144, 126)]
    [InlineData(145, 126)]
    [InlineData(313, 126)]
    // Remaining blocks (:324-367).
    [InlineData(233, 222)]
    [InlineData(120, 154)]
    [InlineData(296, 154)]
    [InlineData(157, 154)]
    [InlineData(160, 154)]
    [InlineData(199, 195)]
    [InlineData(85, 195)]
    [InlineData(336, 310)]
    public void ResolveCanonicalMapId_RemappedZone_ReturnsCanonical(short physical, short canonical)
    {
        Assert.Equal(canonical, ZoneCanonicalGeometryMap.ResolveCanonicalMapId(physical));
    }

    [Theory]
    // Canonical ids resolve to themselves (they are the switch's target, not a remapped case)...
    [InlineData(175)]
    [InlineData(104)]
    [InlineData(126)]
    [InlineData(154)]
    [InlineData(195)]
    [InlineData(310)]
    [InlineData(16)]
    // ...as does any id absent from the switch (the legacy `default` keeps zoneNumber == mServerNumber).
    [InlineData(1)]
    [InlineData(38)]
    [InlineData(49)]
    [InlineData(500)]
    public void ResolveCanonicalMapId_UnmappedOrCanonicalZone_ReturnsInput(short mapId)
    {
        Assert.Equal(mapId, ZoneCanonicalGeometryMap.ResolveCanonicalMapId(mapId));
    }
}
