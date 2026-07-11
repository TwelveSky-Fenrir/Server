using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Tests.Tribes;

public class TribeMigrationGateTests
{
    private static readonly DateTime SaturdayWithinWindow = new(2024, 1, 6, 17, 0, 0);
    private static readonly DateTime WednesdayOutsideWindow = new(2024, 1, 10, 17, 0, 0);

    private static TribeMigrationEligibilityContext OutboundValid()
    {
        return new TribeMigrationEligibilityContext(
            true,
            SaturdayWithinWindow,
            0,
            0,
            TribeMigrationGate.MinLevel,
            0,
            null,
            null,
            null,
            false,
            0,
            [200, 50, 100, 0],
            _ => null);
    }

    private static TribeMigrationEligibilityContext ReturnValid()
    {
        return new TribeMigrationEligibilityContext(
            true,
            WednesdayOutsideWindow,
            TribeMigrationGate.TribeFour,
            1,
            TribeMigrationGate.MinLevel,
            0,
            null,
            null,
            null,
            false,
            1,
            [100, 50, 300, 60],
            _ => null);
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
        var context = ReturnValid() with { NowLocal = WednesdayOutsideWindow };

        Assert.Equal(TribeMigrationOutcome.Success, TribeMigrationGate.Evaluate(context));
    }

    [Theory]
    [InlineData(2024, 1, 6, 16, 0, 0, true)]
    [InlineData(2024, 1, 6, 18, 59, 59, true)]
    [InlineData(2024, 1, 6, 15, 59, 59, false)]
    [InlineData(2024, 1, 6, 19, 0, 0, false)]
    [InlineData(2024, 1, 5, 17, 0, 0, false)]
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
        var context = OutboundValid() with { TribePoints = new[] { 150, 50, 100, 200 } };

        Assert.Equal(TribeMigrationOutcome.NotEligibleByWorldState, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Outbound_OwnTribeIsAllianceAdjustedWeakest_Blocks()
    {
        var context = OutboundValid() with { TribePoints = new[] { 100, 500, 500, 0 } };

        Assert.Equal(TribeMigrationOutcome.NotEligibleByWorldState, TribeMigrationGate.Evaluate(context));
    }

    [Fact]
    public void Evaluate_Outbound_NotRawDominant_Blocks()
    {
        var context = OutboundValid() with
        {
            TribePoints = new[] { 150, 200, 100, 0 },
            AllyOf = tribe => tribe switch { 0 => 1, 1 => 0, _ => null }
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
