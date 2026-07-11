using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Tribes;

public class TribeConversionResolverTests
{
    // Synthetic 3-column equivalence catalog mirroring the real world.Tribe*Equivalences shape:
    //   skill group 0:  tribe0=1000  tribe1=1001  tribe2=1002
    //   item  group 0:  tribe0=2000  tribe1=2001  tribe2=2002
    //   item  group 1:  tribe0=87050 tribe1=87051 tribe2=87052  (base 87000..87128 band, for the +129 V2 test)
    //   cost  group 0:  tribe0=3000  tribe1=3001  tribe2=3002
    private static TribeConversionResolver BuildResolver()
    {
        var skills = new[]
        {
            new TribeSkillEquivalenceRowDto(0, 0, 1000),
            new TribeSkillEquivalenceRowDto(0, 1, 1001),
            new TribeSkillEquivalenceRowDto(0, 2, 1002)
        };

        var items = new[]
        {
            new TribeItemEquivalenceRowDto(0, 0, 2000),
            new TribeItemEquivalenceRowDto(0, 1, 2001),
            new TribeItemEquivalenceRowDto(0, 2, 2002),
            new TribeItemEquivalenceRowDto(1, 0, 87050),
            new TribeItemEquivalenceRowDto(1, 1, 87051),
            new TribeItemEquivalenceRowDto(1, 2, 87052)
        };

        var costumes = new[]
        {
            new TribeCostumeEquivalenceRowDto(0, 0, 3000),
            new TribeCostumeEquivalenceRowDto(0, 1, 3001),
            new TribeCostumeEquivalenceRowDto(0, 2, 3002)
        };

        return new TribeConversionResolver(skills, items, costumes);
    }

    [Theory]
    [InlineData(0, 1, 2000, 2001)]
    [InlineData(0, 2, 2000, 2002)]
    [InlineData(1, 0, 2001, 2000)]
    [InlineData(2, 1, 2002, 2001)]
    public void TryRemapItem_MapsToTargetTribeColumn(byte from, byte to, int itemId, int expected)
    {
        var resolver = BuildResolver();

        var ok = resolver.TryRemapItem(from, to, itemId, out var newItemId);

        Assert.True(ok);
        Assert.Equal(expected, newItemId);
    }

    [Theory]
    [InlineData(0, 1, 87179, 87180)] // 87050+129 -> group1; target tribe1 87051+129
    [InlineData(0, 2, 87179, 87181)] // ... target tribe2 87052+129
    [InlineData(2, 0, 87181, 87179)] // reverse: 87052+129 -> tribe0 87050+129
    public void TryRemapItem_AppliesV2BandOffsetTransparently(byte from, byte to, int itemId, int expected)
    {
        var resolver = BuildResolver();

        var ok = resolver.TryRemapItem(from, to, itemId, out var newItemId);

        Assert.True(ok);
        Assert.Equal(expected, newItemId);
    }

    [Fact]
    public void TryRemapItem_UnmappableItem_ReturnsFalse_AndLeavesOutValueUnchanged()
    {
        var resolver = BuildResolver();

        var ok = resolver.TryRemapItem(0, 1, 9999, out var newItemId);

        Assert.False(ok);
        Assert.Equal(9999, newItemId); // best-effort callers write this back as-is
    }

    [Fact]
    public void TryRemapItem_IsDirectionSpecific_SourceIdMustBelongToSourceTribe()
    {
        var resolver = BuildResolver();

        // 2000 is tribe0's id; asking to remap it AS a tribe1 item finds nothing (tribe1's own id is 2001).
        var ok = resolver.TryRemapItem(1, 2, 2000, out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData(0, 2, 1000, 1002)]
    [InlineData(0, 1, 1000, 1001)]
    [InlineData(2, 0, 1002, 1000)]
    public void TryRemapSkill_MapsToTargetTribeColumn(byte from, byte to, int skillId, int expected)
    {
        var resolver = BuildResolver();

        var ok = resolver.TryRemapSkill(from, to, skillId, out var newSkillId);

        Assert.True(ok);
        Assert.Equal(expected, newSkillId);
    }

    [Fact]
    public void TryRemapSkill_UnmappableSkill_ReturnsFalse_AndLeavesOutValueUnchanged()
    {
        var resolver = BuildResolver();

        var ok = resolver.TryRemapSkill(0, 1, 5555, out var newSkillId);

        Assert.False(ok);
        Assert.Equal(5555, newSkillId);
    }

    [Theory]
    [InlineData(0, 1, 3000, 3001)]
    [InlineData(1, 2, 3001, 3002)]
    public void TryRemapCostume_MapsToTargetTribeColumn(byte from, byte to, int itemId, int expected)
    {
        var resolver = BuildResolver();

        var ok = resolver.TryRemapCostume(from, to, itemId, out var newItemId);

        Assert.True(ok);
        Assert.Equal(expected, newItemId);
    }

    [Theory]
    [InlineData(TribeConversionResolver.BookNobleDragon, TribeConversionResolver.NobleDragon)]
    [InlineData(TribeConversionResolver.BookRoyalSerpent, TribeConversionResolver.RoyalSerpent)]
    [InlineData(TribeConversionResolver.BookGrandTiger, TribeConversionResolver.GrandTiger)]
    public void TryGetBookTargetTribe_DerivesTargetFromItemIdAlone(int bookItemId, byte expectedTribe)
    {
        var resolver = BuildResolver();

        var ok = resolver.TryGetBookTargetTribe(bookItemId, out var toTribe);

        Assert.True(ok);
        Assert.Equal(expectedTribe, toTribe);
    }

    [Theory]
    [InlineData(99013)] // just below the book range
    [InlineData(99017)] // just above the book range
    [InlineData(8153)] // a scroll id, never a book
    [InlineData(0)]
    public void TryGetBookTargetTribe_NonBookItem_ReturnsFalse(int itemId)
    {
        var resolver = BuildResolver();

        var ok = resolver.TryGetBookTargetTribe(itemId, out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData((byte)0, true)]
    [InlineData((byte)1, true)]
    [InlineData((byte)2, true)]
    [InlineData((byte)3, false)] // neutral pool -- no equivalence column, never a conversion target
    [InlineData((byte)255, false)]
    public void IsPlayableTribe_OnlyAcceptsTheThreePlayableTribes(byte tribe, bool expected)
    {
        Assert.Equal(expected, TribeConversionResolver.IsPlayableTribe(tribe));
    }

    [Fact]
    public void AreAllItemsMappable_EveryItemHasAnEquivalent_ReturnsTrue()
    {
        var resolver = BuildResolver();

        // Both a plain item and a V2-band item, all with equivalents in the 0->1 direction.
        var ok = resolver.AreAllItemsMappable(0, 1, new[] { 2000, 87179 });

        Assert.True(ok);
    }

    [Fact]
    public void AreAllItemsMappable_OneItemHasNoEquivalent_ReturnsFalse()
    {
        var resolver = BuildResolver();

        // The book path aborts the WHOLE conversion when any single slot cannot be mapped.
        var ok = resolver.AreAllItemsMappable(0, 1, new[] { 2000, 9999 });

        Assert.False(ok);
    }

    [Fact]
    public void AreAllItemsMappable_EmptyEquipment_ReturnsTrue()
    {
        var resolver = BuildResolver();

        Assert.True(resolver.AreAllItemsMappable(0, 1, Array.Empty<int>()));
    }

    [Fact]
    public void TryRemapItem_RoundTrip_ReturnsOriginalId()
    {
        var resolver = BuildResolver();

        Assert.True(resolver.TryRemapItem(0, 1, 2000, out var forward));
        Assert.True(resolver.TryRemapItem(1, 0, forward, out var back));

        Assert.Equal(2000, back);
    }

    [Fact]
    public void TryRemapItem_V2RoundTrip_PreservesTheOffset()
    {
        var resolver = BuildResolver();

        Assert.True(resolver.TryRemapItem(0, 1, 87179, out var forward));
        Assert.Equal(87180, forward);

        Assert.True(resolver.TryRemapItem(1, 0, forward, out var back));
        Assert.Equal(87179, back);
    }

    [Fact]
    public void Constructor_NullCatalog_Throws()
    {
        var empty = Array.Empty<TribeItemEquivalenceRowDto>();
        var emptySkills = Array.Empty<TribeSkillEquivalenceRowDto>();
        var emptyCostumes = Array.Empty<TribeCostumeEquivalenceRowDto>();

        Assert.Throws<ArgumentNullException>(() => new TribeConversionResolver(null!, empty, emptyCostumes));
        Assert.Throws<ArgumentNullException>(() => new TribeConversionResolver(emptySkills, null!, emptyCostumes));
        Assert.Throws<ArgumentNullException>(() => new TribeConversionResolver(emptySkills, empty, null!));
    }
}
