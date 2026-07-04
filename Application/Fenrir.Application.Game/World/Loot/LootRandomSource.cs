namespace Fenrir.Application.Game.World.Loot;

/// <summary>
///     Ports <c>MyUtil::RandomNumber()</c> (<c>Server/ts25zone/S07_MyGame03.cpp:993-998</c>) exactly: every
///     drop-rate roll compares against this, so its shape (not just its range) is load-bearing.
/// </summary>
/// <remarks>
///     Real formula: <c>(1 + rand%1000) * (1 + rand%1000)</c>, a right-skewed product over [1, 1_000_000] --
///     NOT a uniform draw over [0, 9999]. E.g. item 864's "&lt;= 1000" roll is really ~0.71%, not ~10%.
/// </remarks>
public static class LootRandomSource
{
    /// <summary><c>MyUtil::RandomNumber(void)</c> -- range [1, 1_000_000], right-skewed (see class remarks).</summary>
    public static int RandomNumber(Random random)
    {
        var r1 = random.Next(0, 1000);
        var r2 = random.Next(0, 1000);
        return (1 + r1) * (1 + r2);
    }
}
