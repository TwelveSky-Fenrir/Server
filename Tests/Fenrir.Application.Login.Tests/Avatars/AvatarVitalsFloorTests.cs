using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Tests.Avatars;

// Server/Header/function.h:242-245 (SetIntegerLow) applied at Server/ts25login/S04_MyWork02.cpp:357-358:
// Life floors to 1, Mana floors to 0, each independently.
public class AvatarVitalsFloorTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    public void Clamp_FloorsLifeToAtLeastOne(int storedLife, int expectedLife)
    {
        var (life, _) = AvatarVitalsFloor.Clamp(storedLife, 50);

        Assert.Equal(expectedLife, life);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(-100, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    public void Clamp_FloorsManaToAtLeastZero(int storedMana, int expectedMana)
    {
        var (_, mana) = AvatarVitalsFloor.Clamp(100, storedMana);

        Assert.Equal(expectedMana, mana);
    }

    [Fact]
    public void Clamp_BothAlreadyAboveFloor_ReturnsBothUnchanged()
    {
        var result = AvatarVitalsFloor.Clamp(850, 320);

        Assert.Equal((850, 320), result);
    }

    [Fact]
    public void Clamp_BothBelowFloor_ClampsBothIndependently()
    {
        var result = AvatarVitalsFloor.Clamp(-10, -10);

        Assert.Equal((1, 0), result);
    }
}
