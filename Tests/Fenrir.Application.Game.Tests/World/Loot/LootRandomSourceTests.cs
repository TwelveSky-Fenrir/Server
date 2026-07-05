using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class LootRandomSourceTests
{
    [Fact]
    public void RandomNumber_BothDrawsZero_ReturnsOne()
    {
        // (1 + 0) * (1 + 0) = 1, the formula's own minimum.
        var random = new ScriptedRandom(0, 0);

        Assert.Equal(1, LootRandomSource.RandomNumber(random));
    }

    [Fact]
    public void RandomNumber_BothDrawsMax_ReturnsOneMillion()
    {
        // (1 + 999) * (1 + 999) = 1_000_000, the formula's own maximum.
        var random = new ScriptedRandom(999, 999);

        Assert.Equal(1_000_000, LootRandomSource.RandomNumber(random));
    }

    [Fact]
    public void RandomNumber_IsAProductOfTwoDrawsNotASingleUniformDraw()
    {
        // MyUtil::RandomNumber (S07_MyGame03.cpp): (1+r1)*(1+r2), not a single uniform draw
        var random = new ScriptedRandom(9, 4);

        Assert.Equal(50, LootRandomSource.RandomNumber(random)); // (1+9) * (1+4) = 50
    }

    [Fact]
    public void RandomNumber_NeverExceedsOneMillionOrGoesBelowOne_AcrossManySamples()
    {
        var random = new Random(12345);

        for (var i = 0; i < 10_000; i++)
        {
            var value = LootRandomSource.RandomNumber(random);
            Assert.InRange(value, 1, 1_000_000);
        }
    }

    /// <summary>
    ///     Scripted <see cref="Random" /> subclass -- <see cref="Random.Next(int, int)" /> is virtual precisely for this
    ///     kind of deterministic test double.
    /// </summary>
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
}
