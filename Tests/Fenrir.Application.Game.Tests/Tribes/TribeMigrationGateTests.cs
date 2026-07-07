using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Tests.Tribes;

public class TribeMigrationGateTests
{
    // Saturday 2024-01-06 -- a fixed, known Saturday for every "within window" fixture.
    private static readonly DateTime SaturdayWithinWindow = new(2024, 1, 6, 17, 0, 0);
    private static readonly DateTime WednesdayOutsideWindow = new(2024, 1, 10, 17, 0, 0);

    private static TribeMigrationEligibilityContext OutboundValid()
    {
        // Tribe 0: raw 200 (unique max among 0/1/2, >=100), alliance-adjusted 200 (no allies) is also the
        // unique max, and tribe 3's own 0 points is nowhere near that bloc -- every outbound gate passes.
        return new TribeMigrationEligibilityContext(
            FeatureEnabled: true,
            NowLocal: SaturdayWithinWindow,
            CurrentTribe: 0,
            PreviousTribe: 0,
            Level: TribeMigrationGate.MinLevel,
            TribeRole: 0,
            GuildId: null,
            TeacherCharacterId: null,
            StudentCharacterId: null,
            HasAnyFriend: false,
            ReturnAllowance: 0,
            TribePoints: [200, 50, 100, 0],
            AllyOf: _ => null);
    }

    private static TribeMigrationEligibilityContext ReturnValid()
    {
        // Tribe 3 (current), origin tribe 1 -- origin's 50 points does not exceed tribe 3's own 60, and one
        // banked return allowance is available.
        return new TribeMigrationEligibilityContext(
            FeatureEnabled: true,
            NowLocal: WednesdayOutsideWindow, // deliberately NOT a Saturday -- the return branch must not care
            CurrentTribe: TribeMigrationGate.TribeFour,
            PreviousTribe: 1,
            Level: TribeMigrationGate.MinLevel,
            TribeRole: 0,
            GuildId: null,
            TeacherCharacterId: null,
            StudentCharacterId: null,
            HasAnyFriend: false,
            ReturnAllowance: 1,
            TribePoints: [100, 50, 300, 60],
            AllyOf: _ => null);
    }

    [Fact]
    public void Evaluate_OutboundHappyPath_Succeeds()
    {
        Assert.Equal(TribeMigrationOutcome.Success, TribeMigrationGate.Evaluate(OutboundValid()));
    }

    [Fact]
    public void Evaluate_ReturnHappyPath_Succeeds()
    {
        Assert.Equal(TribeMigrationOutcome.Success, TribeMigrationGate.Evaluate(ReturnValid()));
    }

    [Fact]
    public void Evaluate_FeatureDisabled_BlocksBothBranches()
    {
        var outbound = OutboundValid() with { FeatureEnabled = false };
        var @return = ReturnValid() with { FeatureEnabled = false };

        Assert.Equal(TribeMigrationOutcome.FeatureDisabled, TribeMigrationGate.Evaluate(outbound));
        Assert.Equal(TribeMigrationOutcome.FeatureDisabled, TribeMigrationGate.Evaluate(@return));
    }

    [Fact]
    public void Evaluate_Outbound_OutsideConversionWindow_Blocks()
    {
        var context = OutboundValid() with { NowLocal = WednesdayOutsideWindow };

        Assert.Equal(TribeMigrationOutcome.OutsideConversionWindow, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Return_IgnoresConversionWindow()
    {
        // ReturnValid() is already fixtured with a non-Saturday NowLocal and still succeeds -- the return
        // branch is never time-gated (contract: "only when the character is not already in tribe 3").
        var context = ReturnValid() with { NowLocal = WednesdayOutsideWindow };

        Assert.Equal(TribeMigrationOutcome.Success, TribeMigrationGate.Evaluate(context));
    }

    [Theory]
    [InlineData(2024, 1, 6, 16, 0, 0, true)] // Saturday, window open
    [InlineData(2024, 1, 6, 18, 59, 59, true)] // Saturday, last legal second
    [InlineData(2024, 1, 6, 15, 59, 59, false)] // Saturday, one second too early
    [InlineData(2024, 1, 6, 19, 0, 0, false)] // Saturday, one hour too late
    [InlineData(2024, 1, 5, 17, 0, 0, false)] // Friday, same hour
    public void IsWithinConversionWindow_MatchesSaturday16To18Inclusive(int year, int month, int day, int hour,
        int minute, int second, bool expected)
    {
        var now = new DateTime(year, month, day, hour, minute, second);

        Assert.Equal(expected, TribeMigrationGate.IsWithinConversionWindow(now));
    }

    [Theory]
    [InlineData((short)144)]
    [InlineData((short)0)]
    public void Evaluate_LevelBelowMinimum_BlocksBothBranches(short level)
    {
        var outbound = OutboundValid() with { Level = level };
        var @return = ReturnValid() with { Level = level };

        Assert.Equal(TribeMigrationOutcome.LevelTooLow, TribeMigrationGate.Evaluate(outbound));
        Assert.Equal(TribeMigrationOutcome.LevelTooLow, TribeMigrationGate.Evaluate(@return));
    }

    [Fact]
    public void Evaluate_Outbound_Tribe1_CannotJoinTribeFour()
    {
        var context = OutboundValid() with { CurrentTribe = 1 };

        Assert.Equal(TribeMigrationOutcome.Tribe1CannotJoinTribeFour, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Outbound_OwnTribePointsBelowThreshold_Blocks()
    {
        var context = OutboundValid() with { TribePoints = new[] { 99, 0, 0, 0 } };

        Assert.Equal(TribeMigrationOutcome.TribePointsBelowThreshold, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Outbound_TribeThreeAlreadyAtOrAheadOfStrongestBloc_Blocks()
    {
        // Tribe 0's raw 150 would otherwise be dominant, but tribe 3 already holds 200 -- at least as strong
        // as the strongest combat-tribe bloc -- so CheckPossibleChangeToTribe4's "tribe 3 already dominant"
        // branch fires first.
        var context = OutboundValid() with { TribePoints = new[] { 150, 50, 100, 200 } };

        Assert.Equal(TribeMigrationOutcome.NotEligibleByWorldState, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Outbound_OwnTribeIsAllianceAdjustedWeakest_Blocks()
    {
        // Own tribe clears the raw >=100 floor but is strictly behind both rivals once alliance adjustment is
        // applied (here, no alliances at all -- raw IS the alliance-adjusted total).
        var context = OutboundValid() with { TribePoints = new[] { 100, 500, 500, 0 } };

        Assert.Equal(TribeMigrationOutcome.NotEligibleByWorldState, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Outbound_NotRawDominant_Blocks()
    {
        // Tribe 0 (150) is allied with tribe 1 (200): alliance-adjusted totals tie at 350 each, so the
        // alliance-adjusted world-state check passes (neither "tribe 3 dominant" nor "uniquely weakest"), but
        // tribe 0's own RAW total (150) is still behind tribe 1's raw total (200) -- ReturnBigTribe fails.
        var context = OutboundValid() with
        {
            TribePoints = new[] { 150, 200, 100, 0 },
            AllyOf = tribe => tribe switch { 0 => (byte)1, 1 => (byte)0, _ => null }
        };

        Assert.Equal(TribeMigrationOutcome.NotDominantTribe, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Return_OriginTribeAheadOfTribeThree_Blocks()
    {
        var context = ReturnValid() with { TribePoints = new[] { 0, 100, 0, 50 } };

        Assert.Equal(TribeMigrationOutcome.OriginTribeAheadOfTribeThree, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Return_OriginTribeExactlyTiedWithTribeThree_Succeeds()
    {
        // The contract blocks the return specifically when the origin is STRICTLY ahead -- a tie is allowed.
        var context = ReturnValid() with { TribePoints = new[] { 0, 100, 0, 100 } };

        Assert.Equal(TribeMigrationOutcome.Success, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Return_NoBankedAllowance_Blocks()
    {
        var context = ReturnValid() with { ReturnAllowance = 0 };

        Assert.Equal(TribeMigrationOutcome.NoReturnAllowance, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Return_AllowanceCannotBeReusedAfterItIsSpent()
    {
        // Proves the one-shot/no-farming property at the gate level: a single banked charge is enough for
        // exactly one accepted return, and re-evaluating with the post-decrement allowance (0) blocks the
        // very next attempt outright, regardless of every other input being unchanged and still eligible.
        var oneCharge = ReturnValid() with { ReturnAllowance = 1 };
        Assert.Equal(TribeMigrationOutcome.Success, TribeMigrationGate.Evaluate(oneCharge));

        var spent = oneCharge with { ReturnAllowance = 0 };
        Assert.Equal(TribeMigrationOutcome.NoReturnAllowance, TribeMigrationGate.Evaluate(spent));
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)250)]
    public void Evaluate_Return_PreviousTribeOutOfRange_BlocksDefensively(byte previousTribe)
    {
        var context = ReturnValid() with { PreviousTribe = previousTribe, TribePoints = new[] { 0, 0, 0, 0 } };

        Assert.Equal(TribeMigrationOutcome.InvalidPreviousTribe, TribeMigrationGate.Evaluate(context));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void Evaluate_HoldingAnyTribeRoleTier_BlocksBothBranches(byte tribeRole)
    {
        var outbound = OutboundValid() with { TribeRole = tribeRole };
        var @return = ReturnValid() with { TribeRole = tribeRole };

        Assert.Equal(TribeMigrationOutcome.HoldsTribeRole, TribeMigrationGate.Evaluate(outbound));
        Assert.Equal(TribeMigrationOutcome.HoldsTribeRole, TribeMigrationGate.Evaluate(@return));
    }

    [Fact]
    public void Evaluate_HasGuild_BlocksBothBranches()
    {
        var outbound = OutboundValid() with { GuildId = 7 };
        var @return = ReturnValid() with { GuildId = 7 };

        Assert.Equal(TribeMigrationOutcome.HasGuildOrMentorLink, TribeMigrationGate.Evaluate(outbound));
        Assert.Equal(TribeMigrationOutcome.HasGuildOrMentorLink, TribeMigrationGate.Evaluate(@return));
    }

    [Fact]
    public void Evaluate_HasTeacher_Blocks()
    {
        var context = OutboundValid() with { TeacherCharacterId = 55 };

        Assert.Equal(TribeMigrationOutcome.HasGuildOrMentorLink, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_HasStudent_Blocks()
    {
        var context = OutboundValid() with { StudentCharacterId = 55 };

        Assert.Equal(TribeMigrationOutcome.HasGuildOrMentorLink, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_HasRegisteredFriend_BlocksBothBranches()
    {
        var outbound = OutboundValid() with { HasAnyFriend = true };
        var @return = ReturnValid() with { HasAnyFriend = true };

        Assert.Equal(TribeMigrationOutcome.HasRegisteredFriends, TribeMigrationGate.Evaluate(outbound));
        Assert.Equal(TribeMigrationOutcome.HasRegisteredFriends, TribeMigrationGate.Evaluate(@return));
    }

    [Theory]
    [InlineData(TribeMigrationOutcome.FeatureDisabled, true)]
    [InlineData(TribeMigrationOutcome.OutsideConversionWindow, true)]
    [InlineData(TribeMigrationOutcome.Tribe1CannotJoinTribeFour, true)]
    [InlineData(TribeMigrationOutcome.Success, false)]
    [InlineData(TribeMigrationOutcome.LevelTooLow, false)]
    [InlineData(TribeMigrationOutcome.TribePointsBelowThreshold, false)]
    [InlineData(TribeMigrationOutcome.NotEligibleByWorldState, false)]
    [InlineData(TribeMigrationOutcome.NotDominantTribe, false)]
    [InlineData(TribeMigrationOutcome.OriginTribeAheadOfTribeThree, false)]
    [InlineData(TribeMigrationOutcome.NoReturnAllowance, false)]
    [InlineData(TribeMigrationOutcome.InvalidPreviousTribe, false)]
    [InlineData(TribeMigrationOutcome.HoldsTribeRole, false)]
    [InlineData(TribeMigrationOutcome.HasGuildOrMentorLink, false)]
    [InlineData(TribeMigrationOutcome.HasRegisteredFriends, false)]
    [InlineData(TribeMigrationOutcome.QuotaExhausted, false)]
    public void RepliesWithFailure_MatchesOnlyTheThreeLegacyReplyGates(TribeMigrationOutcome outcome, bool expected)
    {
        Assert.Equal(expected, outcome.RepliesWithFailure());
    }
}
