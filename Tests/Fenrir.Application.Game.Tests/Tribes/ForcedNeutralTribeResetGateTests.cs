using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Tests.Tribes;

/// <summary>
///     Pure, I/O-free coverage of <see cref="ForcedNeutralTribeResetGate" /> (item 8100's precondition chain)
///     -- every gate in isolation, plus the fixed evaluation order the legacy source itself checks in
///     (level → already-neutral → tribe role → guild/mentor → friends → neutral-home-zone-online).
/// </summary>
public class ForcedNeutralTribeResetGateTests
{
    private static ForcedNeutralTribeResetEligibilityContext Eligible()
    {
        return new ForcedNeutralTribeResetEligibilityContext(
            Level: ForcedNeutralTribeResetGate.MinLevel,
            CurrentTribe: 0,
            TribeRole: 0,
            GuildId: null,
            TeacherCharacterId: null,
            StudentCharacterId: null,
            HasAnyFriend: false,
            NeutralHomeZoneOnline: true);
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
    [InlineData((byte)1)] // tribe master
    [InlineData((byte)2)] // sub-master
    [InlineData((byte)3)] // elected vote candidate
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
        // Both LevelTooLow and AlreadyNeutral apply; legacy checks level first (S04_MyWork03.cpp:7226 before
        // :7231-7235), so LevelTooLow must win.
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
        // Every other gate still passes -- confirms this is the terminal check, not merely "some" check.
        Assert.Equal(ForcedNeutralTribeResetOutcome.NeutralHomeZoneOffline,
            ForcedNeutralTribeResetGate.Evaluate(ctx));
    }
}
