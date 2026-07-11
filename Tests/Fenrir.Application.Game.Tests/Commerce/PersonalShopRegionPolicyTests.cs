using Fenrir.Application.Game.Domain.Commerce;

namespace Fenrir.Application.Game.Tests.Commerce;

/// <summary>Covers <see cref="PersonalShopRegionPolicy" />: the 5-region per-tick personal-shop re-check table (contract C21§E).</summary>
public class PersonalShopRegionPolicyTests
{
    [Theory]
    [InlineData((short)1, (byte)0, 4f, 0f, -2f)]
    [InlineData((short)6, (byte)1, -189f, 0f, 1150f)]
    [InlineData((short)11, (byte)2, 449f, 1f, 439f)]
    [InlineData((short)140, (byte)3, 452f, 0f, 487f)]
    [InlineData((short)37, (byte)0, 1f, 0f, -1478f)] // zone 37 applies no tribe restriction
    [InlineData((short)37, (byte)3, 1f, 0f, -1478f)]
    public void AtRegionCenter_WithMatchingTribe_IsPermitted(short mapId, byte tribe, float x, float y, float z)
    {
        Assert.True(PersonalShopRegionPolicy.IsWithinPermittedRegion(mapId, tribe, x, y, z));
    }

    [Theory]
    [InlineData((short)1, (byte)1)] // zone 1 requires tribe 0
    [InlineData((short)6, (byte)0)] // zone 6 requires tribe 1
    [InlineData((short)11, (byte)0)] // zone 11 requires tribe 2
    [InlineData((short)140, (byte)0)] // zone 140 requires tribe 3
    public void AtRegionCenter_WithWrongTribe_IsRejected(short mapId, byte wrongTribe)
    {
        // Position doesn't matter here -- the tribe mismatch alone must fail the check even at dead center.
        Assert.False(PersonalShopRegionPolicy.IsWithinPermittedRegion(mapId, wrongTribe, 0f, 0f, 0f));
    }

    [Fact]
    public void OutsideRadius_IsRejected()
    {
        // Zone 37 center is (1, 0, -1478), radius 1000 -- 1001 units away on one axis alone is already outside.
        Assert.False(PersonalShopRegionPolicy.IsWithinPermittedRegion(37, 0, 1f, 0f, -1478f + 1001f));
    }

    [Fact]
    public void ExactlyOnTheBoundary_IsRejected()
    {
        // Strict less-than: exactly 1000 units away must fail, matching ProxyShopZonePolicy's own analog.
        Assert.False(PersonalShopRegionPolicy.IsWithinPermittedRegion(37, 0, 1f, 0f, -1478f + 1000f));
    }

    [Fact]
    public void JustInsideTheBoundary_IsPermitted()
    {
        Assert.True(PersonalShopRegionPolicy.IsWithinPermittedRegion(37, 0, 1f, 0f, -1478f + 999f));
    }

    [Theory]
    [InlineData((short)2)]
    [InlineData((short)0)]
    [InlineData((short)200)]
    public void MapIdOutsideTheFive_AlwaysFails_RegardlessOfPositionOrTribe(short unknownMapId)
    {
        Assert.False(PersonalShopRegionPolicy.IsWithinPermittedRegion(unknownMapId, 0, 4f, 0f, -2f));
    }
}
