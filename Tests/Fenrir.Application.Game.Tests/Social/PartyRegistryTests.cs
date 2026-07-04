using Fenrir.Application.Game.Social.Party;

namespace Fenrir.Application.Game.Tests.Social;

public class PartyRegistryTests
{
    [Fact]
    public void TryInvite_FirstAccept_CreatesTwoMemberParty()
    {
        var registry = new PartyRegistry();

        var outcome = registry.TryInvite(1, 10, 0, 2, 10, 0);
        Assert.Equal(PartyInviteOutcome.Sent, outcome);

        var accepted = registry.TryAnswer(2, accepted: true, out var inviterId, out var joinOutcome);

        Assert.True(accepted);
        Assert.Equal(1, inviterId);
        Assert.Equal(PartyJoinOutcome.Created, joinOutcome);
        Assert.Equal(new[] { 1, 2 }, registry.GetMembers(1));
        Assert.True(registry.IsLeader(1));
        Assert.True(registry.IsInParty(2));
    }

    [Fact]
    public void TryInvite_DifferentTribe_ReturnsInviterMustDisconnect()
    {
        var registry = new PartyRegistry();

        var outcome = registry.TryInvite(1, 10, 0, 2, 10, 1);

        Assert.Equal(PartyInviteOutcome.InviterMustDisconnect, outcome);
    }

    [Fact]
    public void TryInvite_LevelGapTooLarge_ReturnsInviterMustDisconnect()
    {
        var registry = new PartyRegistry();

        var outcome = registry.TryInvite(1, 10, 0, 2, 20, 0);

        Assert.Equal(PartyInviteOutcome.InviterMustDisconnect, outcome);
    }

    [Fact]
    public void TryInvite_TargetAlreadyPartied_ReturnsTargetAlreadyPartied()
    {
        var registry = new PartyRegistry();
        registry.TryInvite(1, 10, 0, 2, 10, 0);
        registry.TryAnswer(2, true, out _, out _);

        var outcome = registry.TryInvite(3, 10, 0, 2, 10, 0);

        Assert.Equal(PartyInviteOutcome.TargetAlreadyPartied, outcome);
    }

    [Fact]
    public void TryInvite_AlreadyNegotiating_ReturnsBusyForBothSides()
    {
        var registry = new PartyRegistry();
        registry.TryInvite(1, 10, 0, 2, 10, 0);

        Assert.Equal(PartyInviteOutcome.InviterBusy, registry.TryInvite(1, 10, 0, 3, 10, 0));
        Assert.Equal(PartyInviteOutcome.TargetBusy, registry.TryInvite(4, 10, 0, 2, 10, 0));
    }

    [Fact]
    public void TryInvite_TargetAlreadyPartiedAndTribeMismatch_PrefersTargetAlreadyPartied()
    {
        // Check order verified against PARTY_ASK_SEND (S04_MyWork02.cpp:9563-9614): target-already-partied
        // (reply code 6, a soft/clean outcome) is checked BEFORE the tribe-mismatch Quit() -- a prior pass
        // here checked tribe/level first, so an input where both conditions held simultaneously would
        // incorrectly disconnect the inviter instead of sending the soft "already partied" reply the legacy
        // sends in that exact situation.
        var registry = new PartyRegistry();
        registry.TryInvite(1, 10, 0, 2, 10, 0);
        registry.TryAnswer(2, true, out _, out _); // character 2 is now partied, tribe 0

        // Character 3 (a DIFFERENT tribe, 1) invites character 2 (already partied, tribe 0) -- both
        // "target already partied" and "tribe mismatch" are simultaneously true.
        var outcome = registry.TryInvite(3, 10, 1, 2, 10, 0);

        Assert.Equal(PartyInviteOutcome.TargetAlreadyPartied, outcome);
    }

    [Fact]
    public void Join_FifthMember_SucceedsAndSixthIsSilentlyDropped()
    {
        var registry = new PartyRegistry();
        registry.TryInvite(1, 10, 0, 2, 10, 0);
        registry.TryAnswer(2, true, out _, out _);

        for (var member = 3; member <= 6; member++)
        {
            registry.TryInvite(1, 10, 0, member, 10, 0);
            registry.TryAnswer(member, true, out _, out var joinOutcome);

            if (member <= 5)
                Assert.Equal(PartyJoinOutcome.Joined, joinOutcome);
            else
                Assert.Equal(PartyJoinOutcome.PartyWasFull, joinOutcome);
        }

        Assert.Equal(PartyRegistry.MaxMembers, registry.GetMembers(1).Count);
        Assert.False(registry.IsInParty(6));
    }

    [Fact]
    public void TryLeave_LeaderCannotLeave()
    {
        var registry = new PartyRegistry();
        registry.TryInvite(1, 10, 0, 2, 10, 0);
        registry.TryAnswer(2, true, out _, out _);

        var left = registry.TryLeave(1, out _, out _);

        Assert.False(left);
        Assert.Equal(2, registry.GetMembers(1).Count);
    }

    [Fact]
    public void TryLeave_DropsToOneMember_AutoDisbands()
    {
        var registry = new PartyRegistry();
        registry.TryInvite(1, 10, 0, 2, 10, 0);
        registry.TryAnswer(2, true, out _, out _);

        var left = registry.TryLeave(2, out var membersBefore, out var disbanded);

        Assert.True(left);
        Assert.True(disbanded);
        Assert.Equal(new[] { 1, 2 }, membersBefore);
        Assert.False(registry.IsInParty(1));
        Assert.False(registry.IsInParty(2));
    }

    [Fact]
    public void TryKick_NonLeader_Fails()
    {
        var registry = new PartyRegistry();
        registry.TryInvite(1, 10, 0, 2, 10, 0);
        registry.TryAnswer(2, true, out _, out _);

        var kicked = registry.TryKick(2, 1, out _, out _);

        Assert.False(kicked);
    }

    [Fact]
    public void Disband_RemovesEveryMember()
    {
        var registry = new PartyRegistry();
        registry.TryInvite(1, 10, 0, 2, 10, 0);
        registry.TryAnswer(2, true, out _, out _);
        registry.TryInvite(1, 10, 0, 3, 10, 0);
        registry.TryAnswer(3, true, out _, out _);

        var members = registry.Disband(1);

        Assert.Equal(new[] { 1, 2, 3 }, members);
        Assert.False(registry.IsInParty(1));
        Assert.False(registry.IsInParty(2));
        Assert.False(registry.IsInParty(3));
    }

    [Fact]
    public void TryCancel_WithoutPendingAsk_ReturnsFalse()
    {
        var registry = new PartyRegistry();

        Assert.False(registry.TryCancel(1, out _));
    }

    [Fact]
    public void TryAnswer_Refused_ClearsNegotiationWithoutJoining()
    {
        var registry = new PartyRegistry();
        registry.TryInvite(1, 10, 0, 2, 10, 0);

        var answered = registry.TryAnswer(2, accepted: false, out var inviterId, out _);

        Assert.True(answered);
        Assert.Equal(1, inviterId);
        Assert.False(registry.IsInParty(1));
        Assert.False(registry.IsInParty(2));
    }
}
