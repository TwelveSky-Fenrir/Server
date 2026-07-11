using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     wave13/B5-rune-encoders: the general Change/Reset/Set encoder catalog (Server/Header/function.h:345-447)
///     -- the mirror-image encode direction of <see cref="ItemValueCodec.Decode" />.
/// </summary>
public class ItemStateEncoderTests
{
    // ---- Additive (Change*) family: adds a delta onto the existing byte, preserves the other three ----

    [Fact]
    public void ChangeEnchant_AddsDeltaOntoExistingByte_PreservesTheOtherThree()
    {
        var packed = ItemValueCodec.Encode(10, 20, 30, 40);

        var updated = ItemStateEncoder.ChangeEnchant(packed, 5);

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(updated);
        Assert.Equal(15, enchant);
        Assert.Equal(20, combine);
        Assert.Equal(30, refine);
        Assert.Equal(40, socket);
    }

    [Fact]
    public void ChangeCombine_AddsDeltaOntoExistingByte_PreservesTheOtherThree()
    {
        var packed = ItemValueCodec.Encode(10, 20, 30, 40);

        var updated = ItemStateEncoder.ChangeCombine(packed, 3);

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(updated);
        Assert.Equal(10, enchant);
        Assert.Equal(23, combine);
        Assert.Equal(30, refine);
        Assert.Equal(40, socket);
    }

    [Fact]
    public void ChangeRefine_AddsDeltaOntoExistingByte_PreservesTheOtherThree()
    {
        var packed = ItemValueCodec.Encode(10, 20, 30, 40);

        var updated = ItemStateEncoder.ChangeRefine(packed, 2);

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(updated);
        Assert.Equal(10, enchant);
        Assert.Equal(20, combine);
        Assert.Equal(32, refine);
        Assert.Equal(40, socket);
    }

    // ---- Zero-out (Reset*) family: zeroes exactly one byte, preserves the other three ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ResetEachPosition_ZeroesOnlyThatPosition(int position)
    {
        var packed = ItemValueCodec.Encode(10, 20, 30, 40);

        var updated = position switch
        {
            0 => ItemStateEncoder.ResetEnchant(packed),
            1 => ItemStateEncoder.ResetCombine(packed),
            2 => ItemStateEncoder.ResetRefine(packed),
            _ => ItemStateEncoder.ResetSocket(packed)
        };

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(updated);
        var expected = new byte[] { 10, 20, 30, 40 };
        expected[position] = 0;
        Assert.Equal(expected[0], enchant);
        Assert.Equal(expected[1], combine);
        Assert.Equal(expected[2], refine);
        Assert.Equal(expected[3], socket);
    }

    // ---- Direct-assign (Set*, no additive counterpart) family: overwrites one byte outright ----

    [Fact]
    public void SetRefine_OverwritesRefineByteOutright_IgnoringPriorValue_PreservesTheOtherThree()
    {
        var packed = ItemValueCodec.Encode(10, 20, 30, 40);

        var updated = ItemStateEncoder.SetRefine(packed, 99);

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(updated);
        Assert.Equal(10, enchant);
        Assert.Equal(20, combine);
        Assert.Equal(99, refine);
        Assert.Equal(40, socket);
    }

    [Fact]
    public void SetSocket_OverwritesSocketByteOutright_IgnoringPriorValue_PreservesTheOtherThree()
    {
        var packed = ItemValueCodec.Encode(10, 20, 30, 40);

        var updated = ItemStateEncoder.SetSocket(packed, 7);

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(updated);
        Assert.Equal(10, enchant);
        Assert.Equal(20, combine);
        Assert.Equal(30, refine);
        Assert.Equal(7, socket);
    }

    // ---- Full-rebuild (SetAll / SetISIUIMValue): discards the entire prior state ----

    [Fact]
    public void SetAll_DiscardsPriorPackedValue_BuildsFromScratch()
    {
        var priorPacked = ItemValueCodec.Encode(255, 255, 255, 255);

        var rebuilt = ItemStateEncoder.SetAll(1, 2, 3, 4);

        // The prior packed value is irrelevant -- SetAll never reads it.
        Assert.NotEqual(priorPacked, rebuilt);
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(rebuilt);
        Assert.Equal(1, enchant);
        Assert.Equal(2, combine);
        Assert.Equal(3, refine);
        Assert.Equal(4, socket);
    }

    [Fact]
    public void SetAll_SocketDefaultsToZero_WhenOmitted()
    {
        var rebuilt = ItemStateEncoder.SetAll(1, 2, 3);

        var (_, _, _, socket) = ItemValueCodec.Decode(rebuilt);
        Assert.Equal(0, socket);
    }

    [Fact]
    public void SetAll_MatchesItemValueCodecEncode_ForTheSameFourValues()
    {
        Assert.Equal(ItemValueCodec.Encode(45, 6, 0, 0), ItemStateEncoder.SetAll(45, 6, 0, 0));
    }

    /// <summary>
    ///     Specification-level check only (see <see cref="ItemStateEncoder" />'s own remarks on why the
    ///     starter-equip stamp itself is deliberately NOT wired to CreateAvatarService): confirms the
    ///     full-rebuild encoder reproduces the exact packed values the legacy EU33 stamp
    ///     (Server/ts25login/S04_MyWork02.cpp:1100-1168) would have produced for the 6 equip slots
    ///     (Enchant=45, Combine=6, Refine=0, Socket=0) and the cosmetic wing slot (Enchant=40, Combine=0,
    ///     Refine=0, Socket=0).
    /// </summary>
    [Fact]
    public void SetAll_ReproducesLegacyStarterEquipStampLiterals_SpecificationOnly()
    {
        var equipSlotPacked = ItemStateEncoder.SetAll(45, 6, 0, 0);
        var (equipEnchant, equipCombine, equipRefine, equipSocket) = ItemValueCodec.Decode(equipSlotPacked);
        Assert.Equal(45, equipEnchant);
        Assert.Equal(6, equipCombine);
        Assert.Equal(0, equipRefine);
        Assert.Equal(0, equipSocket);

        var wingSlotPacked = ItemStateEncoder.SetAll(40, 0, 0, 0);
        var (wingEnchant, wingCombine, wingRefine, wingSocket) = ItemValueCodec.Decode(wingSlotPacked);
        Assert.Equal(40, wingEnchant);
        Assert.Equal(0, wingCombine);
        Assert.Equal(0, wingRefine);
        Assert.Equal(0, wingSocket);
    }

    // ---- Round-trip byte-overflow hazard: no method here clamps or validates ----

    [Fact]
    public void ChangeEnchant_DeltaOverflowingOneByte_SilentlyWrapsToLowByte()
    {
        var packed = ItemValueCodec.Encode(0, 0, 0, 0);

        // No encoder in this family clamps or validates: a delta that overflows a single byte's storage
        // wraps modulo 256 instead of being rejected or saturated -- exactly the round-trip-safe-range
        // hazard the contract documents (no live legacy call site was found supplying such a delta; this is
        // a documented input-domain constraint, not reproduction of an observed defect).
        var wrappedToZero = ItemStateEncoder.ChangeEnchant(packed, 256);
        var (enchantAt256, _, _, _) = ItemValueCodec.Decode(wrappedToZero);
        Assert.Equal(0, enchantAt256);

        var wrappedFurther = ItemStateEncoder.ChangeEnchant(packed, 300);
        var (enchantAt300, _, _, _) = ItemValueCodec.Decode(wrappedFurther);
        Assert.Equal(44, enchantAt300);
    }
}
