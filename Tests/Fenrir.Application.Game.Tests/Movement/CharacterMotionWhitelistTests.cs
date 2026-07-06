using Fenrir.Application.Game.Domain.Movement;

namespace Fenrir.Application.Game.Tests.Movement;

/// <summary>
///     Covers <see cref="CharacterMotionWhitelist.TryEvaluate" /> -- the full (Sort, Type) legality table and
///     its four resolved outputs, one representative pair per legal Sort plus the illegal-input edge cases.
/// </summary>
public class CharacterMotionWhitelistTests
{
    /// <summary>One row per legal Sort: a legal Type for it, and the exact four outputs the table specifies.</summary>
    [Theory]
    [InlineData(0, 0, 0, true, 0, 0)]
    [InlineData(1, 0, 0, true, 0, 0)]
    [InlineData(1, 7, 0, true, 0, 0)]
    [InlineData(2, 3, 0, true, 0, 0)]
    [InlineData(3, 0, 0, true, 0, 0)]
    [InlineData(3, 4, 0, true, 0, 0)]
    [InlineData(4, 1, 0, true, 0, 0)]
    [InlineData(5, 3, 0, true, 1, 1)]
    [InlineData(6, 5, 0, true, 1, 2)]
    [InlineData(7, 7, 0, true, 1, 3)]
    [InlineData(9, 1, 0, true, 0, 0)]
    [InlineData(10, 6, 0, true, 0, 0)]
    [InlineData(13, 0, 0, true, 0, 0)]
    [InlineData(14, 0, 0, true, 0, 0)]
    [InlineData(15, 0, 0, true, 0, 0)]
    [InlineData(16, 0, 0, true, 0, 0)]
    [InlineData(17, 0, 0, true, 0, 0)]
    [InlineData(18, 0, 0, true, 0, 0)]
    [InlineData(20, 0, 0, true, 0, 0)]
    [InlineData(21, 0, 0, true, 0, 0)]
    [InlineData(22, 0, 0, true, 0, 0)]
    [InlineData(23, 0, 0, true, 0, 0)]
    [InlineData(30, 0, 1, true, 0, 0)]
    [InlineData(32, 0, 2, true, 0, 0)]
    [InlineData(32, 7, 2, true, 0, 0)]
    [InlineData(33, 0, 2, true, 0, 0)]
    [InlineData(38, 0, 2, true, 2, 1)]
    [InlineData(39, 0, 2, true, 2, 1)]
    [InlineData(40, 0, 2, true, 0, 0)]
    [InlineData(41, 0, 2, true, 0, 0)]
    [InlineData(42, 3, 2, true, 3, 1)]
    [InlineData(43, 3, 2, true, 3, 3)]
    [InlineData(44, 3, 2, true, 3, 5)]
    [InlineData(45, 3, 2, true, 3, 1)]
    [InlineData(46, 3, 2, true, 3, 3)]
    [InlineData(48, 5, 2, true, 3, 1)]
    [InlineData(49, 5, 2, true, 3, 3)]
    [InlineData(50, 5, 2, true, 3, 5)]
    [InlineData(51, 5, 2, true, 3, 1)]
    [InlineData(52, 5, 2, true, 3, 3)]
    [InlineData(54, 7, 2, true, 4, 1)]
    [InlineData(55, 7, 2, true, 4, 3)]
    [InlineData(56, 7, 2, true, 3, 5)] // irregular: tag 3, not 4, verbatim per the behavior contract's table
    [InlineData(57, 7, 2, true, 4, 1)]
    [InlineData(58, 7, 2, true, 4, 3)]
    [InlineData(60, 3, 2, true, 0, 0)]
    [InlineData(61, 5, 2, true, 0, 0)]
    [InlineData(62, 7, 2, true, 0, 0)]
    [InlineData(63, 0, 2, true, 0, 0)]
    [InlineData(64, 0, 0, true, 0, 0)]
    [InlineData(65, 0, 0, false, 5, 0)] // enforcement explicitly OFF
    [InlineData(66, 0, 2, true, 0, 0)]
    [InlineData(67, 0, 2, true, 0, 0)]
    [InlineData(68, 0, 2, true, 0, 0)]
    [InlineData(69, 3, 2, true, 3, 1)]
    [InlineData(70, 3, 2, true, 3, 3)]
    [InlineData(71, 5, 2, true, 3, 1)]
    [InlineData(72, 5, 2, true, 3, 3)]
    [InlineData(73, 7, 2, true, 4, 3)]
    [InlineData(74, 7, 2, false, 0, 0)] // enforcement explicitly OFF
    [InlineData(75, 0, 2, true, 0, 0)]
    [InlineData(76, 0, 2, true, 0, 0)]
    [InlineData(81, 3, 2, true, 3, 5)]
    [InlineData(82, 5, 2, true, 3, 5)]
    [InlineData(83, 7, 2, true, 3, 5)]
    [InlineData(85, 3, 2, true, 3, 1)]
    [InlineData(86, 3, 2, true, 3, 3)]
    [InlineData(87, 5, 2, true, 3, 1)]
    [InlineData(88, 5, 2, true, 3, 3)]
    [InlineData(89, 7, 2, true, 4, 1)]
    [InlineData(90, 7, 2, true, 4, 3)]
    [InlineData(91, 0, 0, true, 0, 0)]
    [InlineData(255, 0, 3, true, 0, 0)]
    [InlineData(255, 7, 3, true, 0, 0)]
    public void LegalPair_ResolvesExactOutputs(int sort, int type, int expectedSkillCategoryCode,
        bool expectedEnforced, int expectedTag, int expectedCeiling)
    {
        var resolved = CharacterMotionWhitelist.TryEvaluate(sort, type, out var evaluation);

        Assert.True(resolved);
        Assert.Equal(expectedSkillCategoryCode, evaluation.SkillCategoryCode);
        Assert.Equal(expectedEnforced, evaluation.AttackBudgetEnforced);
        Assert.Equal(expectedTag, evaluation.AttackFamilyTag);
        Assert.Equal(expectedCeiling, evaluation.AttackSubPacketCeiling);
    }

    /// <summary>Every Type 0-7 is legal for a Sort whose row legalizes "all Types" (e.g. Sort 1/2/10/32).</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(32)]
    [InlineData(255)]
    public void AllTypesRow_EveryTypeZeroToSevenIsLegal(int sort)
    {
        for (var type = 0; type <= 7; type++)
            Assert.True(CharacterMotionWhitelist.TryEvaluate(sort, type, out _));
    }

    /// <summary>Sort 3 legalizes only the even Types (0, 2, 4, 6); the odd ones must be rejected.</summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void EvenTypesRow_OnlyEvenTypesAreLegal(int type, bool expectedLegal)
    {
        Assert.Equal(expectedLegal, CharacterMotionWhitelist.TryEvaluate(3, type, out _));
    }

    /// <summary>Sort 4 legalizes only the odd Types (1, 3, 5, 7); the even ones must be rejected.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(7, true)]
    public void OddTypesRow_OnlyOddTypesAreLegal(int type, bool expectedLegal)
    {
        Assert.Equal(expectedLegal, CharacterMotionWhitelist.TryEvaluate(4, type, out _));
    }

    /// <summary>Sort 42 legalizes only Type 3 -- every other Type in 0-7 must be rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void SingleTypeRow_EveryOtherTypeIsRejected(int illegalType)
    {
        Assert.False(CharacterMotionWhitelist.TryEvaluate(42, illegalType, out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(19)]
    [InlineData(24)]
    [InlineData(34)]
    [InlineData(47)]
    [InlineData(53)]
    [InlineData(59)]
    [InlineData(77)]
    [InlineData(78)]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(84)]
    [InlineData(92)]
    [InlineData(254)]
    [InlineData(256)]
    [InlineData(int.MaxValue)]
    public void UnknownSort_IsIllegal_RegardlessOfType(int sort)
    {
        Assert.False(CharacterMotionWhitelist.TryEvaluate(sort, 0, out var evaluation));
        Assert.Equal(default, evaluation);
    }

    /// <summary>
    ///     Sort 31 is an identically-bodied sibling of Sort 30 reachable only under a build flag never turned on
    ///     in any shipped configuration -- dead code, not a legal category, unlike Sort 30 itself.
    /// </summary>
    [Fact]
    public void Sort31_DeadMobileOnlySibling_IsIllegal()
    {
        Assert.False(CharacterMotionWhitelist.TryEvaluate(31, 0, out _));
        Assert.True(CharacterMotionWhitelist.TryEvaluate(30, 0, out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void TypeOutsideZeroToSeven_IsIllegal_EvenForALegalSort(int type)
    {
        Assert.False(CharacterMotionWhitelist.TryEvaluate(1, type, out var evaluation));
        Assert.Equal(default, evaluation);
    }

    [Fact]
    public void EnforcementOffRows_CeilingIsIrrelevant_ButEnforcedIsFalse()
    {
        Assert.True(CharacterMotionWhitelist.TryEvaluate(65, 0, out var sort65));
        Assert.False(sort65.AttackBudgetEnforced);

        Assert.True(CharacterMotionWhitelist.TryEvaluate(74, 7, out var sort74));
        Assert.False(sort74.AttackBudgetEnforced);
    }
}
