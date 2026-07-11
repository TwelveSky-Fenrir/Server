using Fenrir.Application.Game.Domain.AntiCheat;

namespace Fenrir.Application.Game.Tests.AntiCheat;

/// <summary>
///     Covers <see cref="PotionWhileAttackingZoneWhitelist" /> — the 40-zone list transcribed verbatim from
///     CheckPossibleEatPotionAttack (Server/ts25zone/S04_MyWork05.cpp:4995-5071). Pins the transcription;
///     asserts nothing about permit-vs-flag semantics (unresolved — see the type remarks).
/// </summary>
public class PotionWhileAttackingZoneWhitelistTests
{
    private static readonly int[] Listed =
    [
        1, 2, 3, 4, 6, 7, 8, 9, 11, 12, 13, 14, 38, 55, 75, 85, 86, 87, 89, 90,
        99, 100, 125, 140, 141, 142, 143, 195, 196, 197, 198, 199, 201, 270, 271, 272, 273, 274, 295, 296
    ];

    [Fact]
    public void ContainsExactlyFortyZones()
    {
        Assert.Equal(40, PotionWhileAttackingZoneWhitelist.Count);
        Assert.Equal(40, Listed.Length);
    }

    [Fact]
    public void EveryListedZone_IsListed()
    {
        foreach (var zoneId in Listed)
            Assert.True(PotionWhileAttackingZoneWhitelist.IsListed(zoneId), $"zone {zoneId} should be listed");
    }

    [Theory]
    // Gaps deliberately absent from the hand-curated list.
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(200)]
    [InlineData(275)]
    [InlineData(500)]
    [InlineData(-1)]
    public void UnlistedZone_IsNotListed(int zoneId)
    {
        Assert.False(PotionWhileAttackingZoneWhitelist.IsListed(zoneId));
    }
}
