using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeSymbolCombatModifiersTests
{
    [Fact]
    public void FreshInstance_EveryTribeStartsAtZeroPenalty()
    {
        var modifiers = new TribeSymbolCombatModifiers();

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            Assert.Equal(0f, modifiers.GetDamageDownPenalty(tribeId));
    }

    [Fact]
    public void OwnSymbolLostDamageDownPenalty_IsTheCitedFlatTwentyPercent()
    {
        Assert.Equal(0.2f, TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty);
    }

    [Theory]
    [InlineData((byte)WorldStateService.TribeCount)]
    [InlineData((byte)(WorldStateService.TribeCount + 5))]
    public void GetDamageDownPenalty_OutOfRangeTribeId_Throws(byte tribeId)
    {
        var modifiers = new TribeSymbolCombatModifiers();

        Assert.Throws<ArgumentOutOfRangeException>(() => modifiers.GetDamageDownPenalty(tribeId));
    }
}
