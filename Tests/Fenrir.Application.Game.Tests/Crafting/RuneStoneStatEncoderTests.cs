using Fenrir.Application.Game.Domain.Crafting;

namespace Fenrir.Application.Game.Tests.Crafting;

/// <summary>
///     wave13/B5-rune-encoders: the four *ValueRune overwrite encoders (Server/Header/function.h:3348-3386)
///     consumed by the rune-stone-crafting write path -- each proven equivalent to
///     <see cref="RuneStoneCraftResolver" />'s own inline decode-all/replace-one/encode-all computation.
/// </summary>
public class RuneStoneStatEncoderTests
{
    [Fact]
    public void ChangeStrValueRune_OverwritesOnlyStr_PreservesDexVitInt()
    {
        var packed = RuneStoneStatCodec.Encode(5, 6, 7, 8);

        var updated = RuneStoneStatEncoder.ChangeStrValueRune(packed, 30);

        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(updated);
        Assert.Equal(30, str);
        Assert.Equal(6, dex);
        Assert.Equal(7, vit);
        Assert.Equal(8, intel);
    }

    [Fact]
    public void ChangeDexValueRune_OverwritesOnlyDex_PreservesStrVitInt()
    {
        var packed = RuneStoneStatCodec.Encode(5, 6, 7, 8);

        var updated = RuneStoneStatEncoder.ChangeDexValueRune(packed, 30);

        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(updated);
        Assert.Equal(5, str);
        Assert.Equal(30, dex);
        Assert.Equal(7, vit);
        Assert.Equal(8, intel);
    }

    [Fact]
    public void ChangeVitValueRune_OverwritesOnlyVit_PreservesStrDexInt()
    {
        var packed = RuneStoneStatCodec.Encode(5, 6, 7, 8);

        var updated = RuneStoneStatEncoder.ChangeVitValueRune(packed, 30);

        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(updated);
        Assert.Equal(5, str);
        Assert.Equal(6, dex);
        Assert.Equal(30, vit);
        Assert.Equal(8, intel);
    }

    [Fact]
    public void ChangeIntValueRune_OverwritesOnlyInt_PreservesStrDexVit()
    {
        var packed = RuneStoneStatCodec.Encode(5, 6, 7, 8);

        var updated = RuneStoneStatEncoder.ChangeIntValueRune(packed, 30);

        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(updated);
        Assert.Equal(5, str);
        Assert.Equal(6, dex);
        Assert.Equal(7, vit);
        Assert.Equal(30, intel);
    }

    [Fact]
    public void EachEncoder_OverwritesOutright_IgnoringWhateverThePriorValueWas_EvenNegative()
    {
        // The 92298 (single-slot-reroll) branch's own emptiness test allows overwriting a slot whose prior
        // decoded value was already positive (single-slot reroll requires exactly 0, but the encoder itself
        // has no such restriction -- that gating lives in RuneStoneCraftResolver, not here).
        var packed = RuneStoneStatCodec.Encode(-5, -6, -7, -8);

        Assert.Equal(1, RuneStoneStatCodec.Decode(RuneStoneStatEncoder.ChangeStrValueRune(packed, 1)).Str);
        Assert.Equal(2, RuneStoneStatCodec.Decode(RuneStoneStatEncoder.ChangeDexValueRune(packed, 2)).Dex);
        Assert.Equal(3, RuneStoneStatCodec.Decode(RuneStoneStatEncoder.ChangeVitValueRune(packed, 3)).Vit);
        Assert.Equal(4, RuneStoneStatCodec.Decode(RuneStoneStatEncoder.ChangeIntValueRune(packed, 4)).Int);
    }

    // ---- Equivalence with RuneStoneCraftResolver's own inline decode-all/replace-one/encode-all shape ----

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(5, 6, 7, 8)]
    [InlineData(-5, -6, -7, -8)]
    [InlineData(30, 30, 30, 30)]
    public void MatchesResolverStyleDecodeReplaceEncode_ForEveryPosition(sbyte str, sbyte dex, sbyte vit,
        sbyte intel)
    {
        var packed = RuneStoneStatCodec.Encode(str, dex, vit, intel);
        const sbyte roll = 17;

        // The shape RuneStoneCraftResolver itself uses inline (decode all four, re-encode with one replaced).
        var (rStr, rDex, rVit, rInt) = RuneStoneStatCodec.Decode(packed);
        var resolverStyleStr = RuneStoneStatCodec.Encode(roll, rDex, rVit, rInt);
        var resolverStyleDex = RuneStoneStatCodec.Encode(rStr, roll, rVit, rInt);
        var resolverStyleVit = RuneStoneStatCodec.Encode(rStr, rDex, roll, rInt);
        var resolverStyleInt = RuneStoneStatCodec.Encode(rStr, rDex, rVit, roll);

        Assert.Equal(resolverStyleStr, RuneStoneStatEncoder.ChangeStrValueRune(packed, roll));
        Assert.Equal(resolverStyleDex, RuneStoneStatEncoder.ChangeDexValueRune(packed, roll));
        Assert.Equal(resolverStyleVit, RuneStoneStatEncoder.ChangeVitValueRune(packed, roll));
        Assert.Equal(resolverStyleInt, RuneStoneStatEncoder.ChangeIntValueRune(packed, roll));
    }
}
