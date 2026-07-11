using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class AutoHuntEnableGateTests
{
    [Theory]
    [InlineData((short)38)]
    [InlineData((short)319)]
    [InlineData((short)320)]
    [InlineData((short)321)]
    [InlineData((short)322)]
    [InlineData((short)323)]
    [InlineData((short)241)]
    [InlineData((short)249)]
    [InlineData((short)292)]
    [InlineData((short)294)]
    [InlineData((short)311)]
    [InlineData((short)312)]
    [InlineData((short)325)]
    [InlineData((short)330)]
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
    [InlineData((short)240)]
    [InlineData((short)250)]
    [InlineData((short)324)]
    [InlineData((short)331)]
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
            Assert.Contains(mapId, (IEnumerable<short>)AutoHuntEnableGate.BlockedMapNumbers);
    }
}
