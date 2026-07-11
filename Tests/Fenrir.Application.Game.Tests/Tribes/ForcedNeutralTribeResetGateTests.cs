using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Tests.Tribes;

public class ForcedNeutralTribeResetGateTests
{
    private static ForcedNeutralTribeResetEligibilityContext Eligible()
    {
        return new ForcedNeutralTribeResetEligibilityContext(
            ForcedNeutralTribeResetGate.MinLevel,
            0,
            0,
            null,
            null,
            null,
            false,
            true);
    }

    [Fact]
    public void EveryPreconditionMet_Succeeds()
    {
        Assert.Equal(ForcedNeutralTribeResetOutcome.Success, ForcedNeutralTribeResetGate.Evaluate(Eligible()));
    }

    [Fact]
    public void AtExactlyMinLevel_Succeeds()
    {
        var ctx = Eligible() with { Level = 113 };
        Assert.Equal(ForcedNeutralTribeResetOutcome.Success, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void OneBelowMinLevel_FailsWithLevelTooLow()
    {
        var ctx = Eligible() with { Level = 112 };
        Assert.Equal(ForcedNeutralTribeResetOutcome.LevelTooLow, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void AlreadyOnNeutralTribe_FailsWithAlreadyNeutral()
    {
        var ctx = Eligible() with { CurrentTribe = ForcedNeutralTribeResetGate.NeutralTribe };
        Assert.Equal(ForcedNeutralTribeResetOutcome.AlreadyNeutral, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void HoldsAnyTribeRoleTier_FailsWithHoldsTribeRole(byte tribeRole)
    {
        var ctx = Eligible() with { TribeRole = tribeRole };
        Assert.Equal(ForcedNeutralTribeResetOutcome.HoldsTribeRole, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void HasGuild_FailsWithHasGuildOrMentorLink()
    {
        var ctx = Eligible() with { GuildId = 42 };
        Assert.Equal(ForcedNeutralTribeResetOutcome.HasGuildOrMentorLink, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void HasTeacher_FailsWithHasGuildOrMentorLink()
    {
        var ctx = Eligible() with { TeacherCharacterId = 7 };
        Assert.Equal(ForcedNeutralTribeResetOutcome.HasGuildOrMentorLink, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void HasStudent_FailsWithHasGuildOrMentorLink()
    {
        var ctx = Eligible() with { StudentCharacterId = 9 };
        Assert.Equal(ForcedNeutralTribeResetOutcome.HasGuildOrMentorLink, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void HasAnyRegisteredFriend_FailsWithHasRegisteredFriends()
    {
        var ctx = Eligible() with { HasAnyFriend = true };
        Assert.Equal(ForcedNeutralTribeResetOutcome.HasRegisteredFriends, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void NeutralHomeZoneNotOnline_FailsWithNeutralHomeZoneOffline_EvenWhenEveryOtherGatePasses()
    {
        var ctx = Eligible() with { NeutralHomeZoneOnline = false };
        Assert.Equal(ForcedNeutralTribeResetOutcome.NeutralHomeZoneOffline,
            ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void LevelGate_IsCheckedBeforeAlreadyNeutral_MatchingLegacyEvaluationOrder()
    {
        var ctx = Eligible() with { Level = 100, CurrentTribe = ForcedNeutralTribeResetGate.NeutralTribe };
        Assert.Equal(ForcedNeutralTribeResetOutcome.LevelTooLow, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void TribeRoleGate_IsCheckedBeforeGuildMentorAndFriends()
    {
        var ctx = Eligible() with { TribeRole = 1, GuildId = 1, HasAnyFriend = true, NeutralHomeZoneOnline = false };
        Assert.Equal(ForcedNeutralTribeResetOutcome.HoldsTribeRole, ForcedNeutralTribeResetGate.Evaluate(ctx));
    }

    [Fact]
    public void NeutralHomeZoneGate_IsCheckedLast()
    {
        var ctx = Eligible() with { NeutralHomeZoneOnline = false };
        Assert.Equal(ForcedNeutralTribeResetOutcome.NeutralHomeZoneOffline,
            ForcedNeutralTribeResetGate.Evaluate(ctx));
    }
}
