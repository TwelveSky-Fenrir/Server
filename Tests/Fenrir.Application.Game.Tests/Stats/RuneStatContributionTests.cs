using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

public class RuneStatContributionTests
{
    private const int RuneItemId = 93514;
    private static readonly EquippedItemSlot[] NoEquipment = [];

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


    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(127, 127)]
    [InlineData(128, -128)]
    [InlineData(200, -56)]
    [InlineData(255, -1)]
    public void Decoder_Strength_ReadsLowByteAsSignedChar(int lowByte, int expected)
    {
        Assert.Equal(expected, RuneStatDecoder.Strength(lowByte));
    }

    [Fact]
    public void Decoder_MapsEachBytePositionToItsAttribute()
    {
        var packed = 10 | (20 << 8) | (30 << 16) | (40 << 24);

        Assert.Equal(10, RuneStatDecoder.Strength(packed));
        Assert.Equal(20, RuneStatDecoder.Dexterity(packed));
        Assert.Equal(30, RuneStatDecoder.Vitality(packed));
        Assert.Equal(40, RuneStatDecoder.Ki(packed));
    }

    [Fact]
    public void Decoder_HighBytes_AreIndependentAndSigned()
    {
        var packed = 200 << 24;

        Assert.Equal(0, RuneStatDecoder.Strength(packed));
        Assert.Equal(0, RuneStatDecoder.Dexterity(packed));
        Assert.Equal(0, RuneStatDecoder.Vitality(packed));
        Assert.Equal(-56, RuneStatDecoder.Ki(packed));
    }


    [Fact]
    public void RuneStrengthByte_FeedsBaseStrength_ExactlyLikeRawAttribute()
    {
        const int n = 25;
        var attrs = Attrs();

        var raw = StatCalculator.ComputeBaseStats(attrs with { Strength = attrs.Strength + n }, NoEquipment, Levels());

        Assert.NotEqual(Base(attrs), raw);
        Assert.Equal(raw, WithRune(attrs, n));
    }

    [Fact]
    public void RuneDexterityByte_FeedsBaseDexterity_ExactlyLikeRawAttribute()
    {
        const int n = 33;
        var attrs = Attrs();

        var raw = StatCalculator.ComputeBaseStats(attrs with { Dexterity = attrs.Dexterity + n }, NoEquipment,
            Levels());

        Assert.NotEqual(Base(attrs), raw);
        Assert.Equal(raw, WithRune(attrs, n << 8));
    }

    [Fact]
    public void RuneVitalityByte_FeedsBaseVitality_ExactlyLikeRawAttribute()
    {
        const int n = 50;
        var attrs = Attrs();

        var raw = StatCalculator.ComputeBaseStats(attrs with { Vitality = attrs.Vitality + n }, NoEquipment, Levels());

        Assert.NotEqual(Base(attrs), raw);
        Assert.Equal(raw, WithRune(attrs, n << 16));
    }

    [Fact]
    public void RuneKiByte_FeedsBaseIntelligence_ExactlyLikeRawAttribute()
    {
        const int n = 42;
        var attrs = Attrs();

        var raw = StatCalculator.ComputeBaseStats(attrs with { Intelligence = attrs.Intelligence + n }, NoEquipment,
            Levels());

        Assert.NotEqual(Base(attrs), raw);
        Assert.Equal(raw, WithRune(attrs, n << 24));
    }

    [Fact]
    public void SingleSocket_FeedsAllFourBaseAttributesAtOnce_ExactlyLikeRawAttributes()
    {
        var attrs = Attrs();
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


    [Fact]
    public void EmptySocket_IgnoresPackedStatEntirely()
    {
        var attrs = Attrs();
        var cosmetic = new CosmeticContext([0, 0, 0, 0], [0x7F7F7F7F, 0, 0, 0]);

        var withEmpty = StatCalculator.ComputeBaseStats(attrs, NoEquipment, Levels(), cosmetic: cosmetic);

        Assert.Equal(Base(attrs), withEmpty);
    }

    [Fact]
    public void RuneByteAtOrAbove128_ReadsNegative_AndContributesNothing()
    {
        var attrs = Attrs();
        var withHighByte = WithRune(attrs, 200 << 16);

        Assert.Equal(Base(attrs), withHighByte);
    }

    [Fact]
    public void RuneByteOfExactly128_ContributesNothing()
    {
        var attrs = Attrs();
        var withByte128 = WithRune(attrs, 128);

        Assert.Equal(Base(attrs), withByte128);
    }

    [Fact]
    public void AllFourSockets_SumTheirStrengthContributions()
    {
        var attrs = Attrs();
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
        var cosmetic = new CosmeticContext(
            [93514, 93515, 0, 0],
            [15, 130, 0, 0]);

        var raw = StatCalculator.ComputeBaseStats(attrs with { Strength = attrs.Strength + 15 }, NoEquipment,
            Levels());
        var withRunes = StatCalculator.ComputeBaseStats(attrs, NoEquipment, Levels(), cosmetic: cosmetic);

        Assert.Equal(raw, withRunes);
    }
}
