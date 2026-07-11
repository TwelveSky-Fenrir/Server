using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

/// <summary>
///     Covers <see cref="AutoHuntEnableGate" /> -- the CZ_AUTO_CONFIG_SEND enable blocked-zone FrozenSet
///     (S04_MyWork02.cpp:13540-13553).
/// </summary>
public class AutoHuntEnableGateTests
{
    [Theory]
    [InlineData((short)38)] // sacred-stone
    [InlineData((short)319)]
    [InlineData((short)320)]
    [InlineData((short)321)]
    [InlineData((short)322)]
    [InlineData((short)323)]
    [InlineData((short)241)] // start of the 20-number "zone 241 type" set
    [InlineData((short)249)]
    [InlineData((short)292)]
    [InlineData((short)294)]
    [InlineData((short)311)]
    [InlineData((short)312)]
    [InlineData((short)325)]
    [InlineData((short)330)] // end of the "zone 241 type" set
    public void CitedBlockedMapNumbers_AreRefused(short mapId)
    {
        Assert.True(AutoHuntEnableGate.IsEnableBlocked(mapId));
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)37)]
    [InlineData((short)39)]
    [InlineData((short)124)]
    [InlineData((short)126)]
    [InlineData((short)240)] // just below the zone-241 set
    [InlineData((short)250)] // just above 241-249
    [InlineData((short)324)] // between 323 and 325
    [InlineData((short)331)] // just above 330
    public void OrdinaryMaps_AreNotRefused(short mapId)
    {
        Assert.False(AutoHuntEnableGate.IsEnableBlocked(mapId));
    }

    [Fact]
    public void FrozenSet_ContainsEveryCitedNumberAndNothingElse()
    {
        short[] expected =
        [
            38, 319, 320, 321, 322, 323,
            241, 242, 243, 244, 245, 246, 247, 248, 249,
            292, 293, 294,
            311, 312,
            325, 326, 327, 328, 329, 330
        ];

        Assert.Equal(expected.Length, AutoHuntEnableGate.BlockedMapNumbers.Count);
        foreach (var mapId in expected)
            Assert.True(AutoHuntEnableGate.BlockedMapNumbers.Contains(mapId));
    }
}
