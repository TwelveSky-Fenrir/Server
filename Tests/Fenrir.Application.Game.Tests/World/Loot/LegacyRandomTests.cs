using Fenrir.Application.Game.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class LegacyRandomTests
{
    /// <summary>Scripted <see cref="Random" /> subclass -- <see cref="Random.Next(int, int)" /> is virtual precisely for this kind of deterministic test double.</summary>
    private sealed class ScriptedRandom(params int[] sequence) : Random
    {
        private int _index;

        public override int Next(int minValue, int maxValue)
        {
            var value = sequence[_index % sequence.Length];
            _index++;
            return minValue + value % (maxValue - minValue);
        }
    }

    [Fact]
    public void RandomNumber_BothDrawsZero_ReturnsOne()
    {
        // (1 + 0) * (1 + 0) = 1, the formula's own minimum.
        var random = new ScriptedRandom(0, 0);

        Assert.Equal(1, LegacyRandom.RandomNumber(random));
    }

    [Fact]
    public void RandomNumber_BothDrawsMax_ReturnsOneMillion()
    {
        // (1 + 999) * (1 + 999) = 1_000_000, the formula's own maximum.
        var random = new ScriptedRandom(999, 999);

        Assert.Equal(1_000_000, LegacyRandom.RandomNumber(random));
    }

    [Fact]
    public void RandomNumber_IsAProductOfTwoDrawsNotASingleUniformDraw()
    {
        // Verified against source (MyUtil::RandomNumber, S07_MyGame03.cpp:993-998): (1+r1)*(1+r2), NOT a
        // single uniform draw over some fixed range -- this pins the exact, source-verified shape rather than
        // the report's own flagged "0-9999 ?" uncertainty (see LegacyRandom's class remarks).
        var random = new ScriptedRandom(9, 4);

        Assert.Equal(50, LegacyRandom.RandomNumber(random)); // (1+9) * (1+4) = 50
    }

    [Fact]
    public void RandomNumber_NeverExceedsOneMillionOrGoesBelowOne_AcrossManySamples()
    {
        var random = new Random(12345);

        for (var i = 0; i < 10_000; i++)
        {
            var value = LegacyRandom.RandomNumber(random);
            Assert.InRange(value, 1, 1_000_000);
        }
    }
}
