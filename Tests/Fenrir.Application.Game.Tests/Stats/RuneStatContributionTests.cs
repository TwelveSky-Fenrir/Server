using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B5: the rune system's base-attribute feed (<c>Server/Header/Protocol/MyFactor.cpp</c> section
///     4 -- :1685-1695/1744-1754/1803-1813/1862-1872, decoders <c>Server/Header/function.h:317-343</c>). Two
///     layers: <see cref="RuneStatDecoder" />'s exact per-byte, signed-char decode, and the four base-attribute
///     getters consuming it through <see cref="StatCalculator.ComputeBaseStats" />.
///     <para>
///         The integration assertions exploit a truncation-exactness property: legacy adds each rune byte into
///         the base attribute (<c>tStr</c>/<c>tVit</c>/...) BEFORE any <c>(int)</c> derived-stat truncation, so
///         feeding a rune byte of N must produce a result bitwise-identical to raising the raw
///         <see cref="CharacterBaseAttributes" /> value by the same N. Each equivalence is paired with a
///         "raising the raw attribute actually moves a stat under this level row" guard, so an equivalence can
///         never pass vacuously.
///     </para>
/// </summary>
public class RuneStatContributionTests
{
    private const int RuneItemId = 93514; // the rune item family (93514-93517); any id > 0 marks a socket occupied
    private static readonly EquippedItemSlot[] NoEquipment = [];

    // level 100, no equipment: Vit->MaxLife (x20), Ki->MaxMana, Str->AttackPower (x2.25 no-weapon), Wisdom->
    // DefensePower (x9.63) are all live off base attributes alone, so every attribute below is load-bearing.
    private static CharacterBaseAttributes Attrs(int vit = 100, int str = 80, int intel = 60, int dex = 40)
    {
        return new CharacterBaseAttributes(vit, str, intel, dex,
            Level: 100, Tribe: 0, PreviousTribe: 0, Title: 0, Halo: 0, RebirthCount: 0);
    }

    private static FrozenDictionary<short, LevelRowDto> Levels()
    {
        return new Dictionary<short, LevelRowDto>
        {
            [100] = new(100, 0, 100, 0, 250, 300, 40, 35, 20, 500, 400)
        }.ToFrozenDictionary();
    }

    // One occupied socket (socket 0) carrying the given packed stat; the other three empty.
    private static CosmeticContext OneRune(int packedStat)
    {
        return new CosmeticContext([RuneItemId, 0, 0, 0], [packedStat, 0, 0, 0]);
    }

    private static EffectiveStats Base(CharacterBaseAttributes attrs)
    {
        return StatCalculator.ComputeBaseStats(attrs, NoEquipment, Levels());
    }

    private static EffectiveStats WithRune(CharacterBaseAttributes attrs, int packedStat)
    {
        return StatCalculator.ComputeBaseStats(attrs, NoEquipment, Levels(), cosmetic: OneRune(packedStat));
    }

    // ---- RuneStatDecoder: exact signed-byte decode ----

    [Theory]
    [InlineData(0, 0)] // empty byte
    [InlineData(1, 1)]
    [InlineData(127, 127)] // top of the positive signed-byte range
    [InlineData(128, -128)] // >=128 reads back NEGATIVE through the signed char (function.h:317-322)
    [InlineData(200, -56)]
    [InlineData(255, -1)]
    public void Decoder_Strength_ReadsLowByteAsSignedChar(int lowByte, int expected)
    {
        // lowByte in 0..255 -> byte 0 == lowByte, decoded through a signed char.
        Assert.Equal(expected, RuneStatDecoder.Strength(lowByte));
    }

    [Fact]
    public void Decoder_MapsEachBytePositionToItsAttribute()
    {
        // b0=10 (Str), b1=20 (Dex/Wisdom), b2=30 (Vit), b3=40 (Ki/Int).
        var packed = 10 | (20 << 8) | (30 << 16) | (40 << 24);

        Assert.Equal(10, RuneStatDecoder.Strength(packed));
        Assert.Equal(20, RuneStatDecoder.Dexterity(packed));
        Assert.Equal(30, RuneStatDecoder.Vitality(packed));
        Assert.Equal(40, RuneStatDecoder.Ki(packed));
    }

    [Fact]
    public void Decoder_HighBytes_AreIndependentAndSigned()
    {
        // Only byte 3 set to 200 (reads back -56); every other byte 0.
        var packed = 200 << 24;

        Assert.Equal(0, RuneStatDecoder.Strength(packed));
        Assert.Equal(0, RuneStatDecoder.Dexterity(packed));
        Assert.Equal(0, RuneStatDecoder.Vitality(packed));
        Assert.Equal(-56, RuneStatDecoder.Ki(packed));
    }

    // ---- Integration: each byte feeds its base attribute, truncation-exact ----

    [Fact]
    public void RuneStrengthByte_FeedsBaseStrength_ExactlyLikeRawAttribute()
    {
        const int n = 25;
        var attrs = Attrs();

        var raw = StatCalculator.ComputeBaseStats(attrs with { Strength = attrs.Strength + n }, NoEquipment, Levels());

        Assert.NotEqual(Base(attrs), raw); // Strength is load-bearing under this level row
        Assert.Equal(raw, WithRune(attrs, n)); // byte 0 -> Strength, same (int) truncation
    }

    [Fact]
    public void RuneDexterityByte_FeedsBaseDexterity_ExactlyLikeRawAttribute()
    {
        const int n = 33;
        var attrs = Attrs();

        var raw = StatCalculator.ComputeBaseStats(attrs with { Dexterity = attrs.Dexterity + n }, NoEquipment,
            Levels());

        Assert.NotEqual(Base(attrs), raw);
        Assert.Equal(raw, WithRune(attrs, n << 8)); // byte 1 -> Dexterity/Wisdom
    }

    [Fact]
    public void RuneVitalityByte_FeedsBaseVitality_ExactlyLikeRawAttribute()
    {
        const int n = 50;
        var attrs = Attrs();

        var raw = StatCalculator.ComputeBaseStats(attrs with { Vitality = attrs.Vitality + n }, NoEquipment, Levels());

        Assert.NotEqual(Base(attrs), raw);
        Assert.Equal(raw, WithRune(attrs, n << 16)); // byte 2 -> Vitality
    }

    [Fact]
    public void RuneKiByte_FeedsBaseIntelligence_ExactlyLikeRawAttribute()
    {
        const int n = 42;
        var attrs = Attrs();

        var raw = StatCalculator.ComputeBaseStats(attrs with { Intelligence = attrs.Intelligence + n }, NoEquipment,
            Levels());

        Assert.NotEqual(Base(attrs), raw);
        Assert.Equal(raw, WithRune(attrs, n << 24)); // byte 3 -> Intelligence/Ki
    }

    [Fact]
    public void SingleSocket_FeedsAllFourBaseAttributesAtOnce_ExactlyLikeRawAttributes()
    {
        var attrs = Attrs();
        // One socket, four independent bytes: Str+5, Dex+6, Vit+7, Ki+8.
        var packed = 5 | (6 << 8) | (7 << 16) | (8 << 24);

        var raw = StatCalculator.ComputeBaseStats(
            attrs with
            {
                Strength = attrs.Strength + 5,
                Dexterity = attrs.Dexterity + 6,
                Vitality = attrs.Vitality + 7,
                Intelligence = attrs.Intelligence + 8
            }, NoEquipment, Levels());

        Assert.Equal(raw, WithRune(attrs, packed));
    }

    // ---- Guards: empty socket, sign-extension, multi-socket sum ----

    [Fact]
    public void EmptySocket_IgnoresPackedStatEntirely()
    {
        var attrs = Attrs();
        // Socket item id 0 (empty) but a fully-populated packed stat -- the id>0 guard must skip it outright.
        var cosmetic = new CosmeticContext([0, 0, 0, 0], [0x7F7F7F7F, 0, 0, 0]);

        var withEmpty = StatCalculator.ComputeBaseStats(attrs, NoEquipment, Levels(), cosmetic: cosmetic);

        Assert.Equal(Base(attrs), withEmpty);
    }

    [Fact]
    public void RuneByteAtOrAbove128_ReadsNegative_AndContributesNothing()
    {
        var attrs = Attrs();
        // Vitality byte = 200 (>=128 -> reads back -56); the >0 guard drops it, so no change.
        var withHighByte = WithRune(attrs, 200 << 16);

        Assert.Equal(Base(attrs), withHighByte);
    }

    [Fact]
    public void RuneByteOfExactly128_ContributesNothing()
    {
        var attrs = Attrs();
        // 128 reads back as -128; still not strictly > 0.
        var withByte128 = WithRune(attrs, 128);

        Assert.Equal(Base(attrs), withByte128);
    }

    [Fact]
    public void AllFourSockets_SumTheirStrengthContributions()
    {
        var attrs = Attrs();
        // Four occupied sockets, each contributing to Strength (byte 0): 10+20+30+40 = 100.
        var cosmetic = new CosmeticContext(
            [93514, 93515, 93516, 93517],
            [10, 20, 30, 40]);

        var raw = StatCalculator.ComputeBaseStats(attrs with { Strength = attrs.Strength + 100 }, NoEquipment,
            Levels());
        var withRunes = StatCalculator.ComputeBaseStats(attrs, NoEquipment, Levels(), cosmetic: cosmetic);

        Assert.Equal(raw, withRunes);
    }

    [Fact]
    public void OccupiedSockets_MixPositiveAndSuppressedBytes()
    {
        var attrs = Attrs();
        // Socket 0: Str+15 (byte 0 = 15). Socket 1: Str byte = 130 (>=128, suppressed). Sockets 2/3 empty.
        // Net Strength contribution = 15 only.
        var cosmetic = new CosmeticContext(
            [93514, 93515, 0, 0],
            [15, 130, 0, 0]);

        var raw = StatCalculator.ComputeBaseStats(attrs with { Strength = attrs.Strength + 15 }, NoEquipment,
            Levels());
        var withRunes = StatCalculator.ComputeBaseStats(attrs, NoEquipment, Levels(), cosmetic: cosmetic);

        Assert.Equal(raw, withRunes);
    }
}
