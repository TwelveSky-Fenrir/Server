namespace Fenrir.Application.Game.World.Loot;

/// <summary>
///     Ports <c>MyUtil::RandomNumber()</c> (verified against source, <c>Server/ts25zone/S07_MyGame03.cpp:993-998</c>)
///     EXACTLY -- every drop-rate roll in the legacy pipeline (money/potion/general-item/quest/extra, report
///     ServerDocs/30_Fenrir_ServerLogic/05_game_mechanics.md §5) compares a stored rate against this call's
///     result, so its SHAPE (not just its range) is load-bearing.
/// </summary>
/// <remarks>
///     Report 05 §13.3 flags this exact spot as needing verification: "RandomNumber() &lt;= 1000 = 10%
///     suppose une plage précise (0-9999) à vérifier lors du portage des taux". Reading the source directly
///     resolves that flagged doubt: the REAL formula is <c>(1 + rand_mir()%1000) * (1 + rand_mir()%1000)</c>
///     -- a right-skewed PRODUCT of two independent uniforms over [1,1000], ranging over [1, 1_000_000], NOT
///     a uniform draw over [0, 9999]. Concretely this means the unconditional "item 864 at RandomNumber()
///     &lt;= 1000" drop (report 05 §5 item 3, itself annotated there as "≈10%") is actually only
///     <c>D(1000)/1_000_000 ≈ 0.7069%</c> (D(1000) = 7069, the divisor-summatory count of (x,y) pairs in
///     [1,1000]² with x*y ≤ 1000) -- the report's "10%" annotation was the naive 0-9999 assumption it warned
///     about, not the source-verified rate. This port preserves the REAL formula (and therefore the real,
///     much-lower probability) rather than the report's own flagged-uncertain annotation (D8: prefer the
///     verified source over a report's own caveat-carrying guess).
///     <para>
///     The underlying <c>rand_mir()</c> generator's exact bit-stream does not matter (report §13.3: "la
///     distribution exacte importe peu, mais les seuils ... supposent une plage précise à vérifier") -- only
///     this product SHAPE does -- so a standard PRNG stands in for it here.
///     </para>
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
