using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Tests.Social;

public class PartyIdentityResolverTests
{
    [Fact]
    public void NotPartied_ResolvesToEmpty()
    {
        var result = PartyIdentityResolver.ResolveCurrentPartyName([], 1, "Solo", static _ => null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void RequesterIsLeader_ResolvesToOwnName_NoLookupNeeded()
    {
        var lookupCalled = false;
        string? TryResolve(int _)
        {
            lookupCalled = true;
            return "ShouldNeverBeUsed";
        }

        var result = PartyIdentityResolver.ResolveCurrentPartyName([10, 11], 10, "Leader", TryResolve);

        Assert.Equal("Leader", result);
        Assert.False(lookupCalled);
    }

    [Fact]
    public void RequesterIsNonLeaderMember_ResolvesToLeadersName_WhenLeaderIsResolvable()
    {
        var result = PartyIdentityResolver.ResolveCurrentPartyName([10, 11], 11, "Mate",
            id => id == 10 ? "Leader" : null);

        Assert.Equal("Leader", result);
    }

    [Fact]
    public void RequesterIsNonLeaderMember_FallsBackToOwnName_WhenLeaderIsNotLocallyResolvable()
    {
        // Same posture as MonsterSpawnScheduler.ResolvePartyDrop's cross-zone fallback: still non-empty
        // (still marks the requester as partied for comparison purposes), just not byte-identical to the
        // true leader name in this rare edge case.
        var result = PartyIdentityResolver.ResolveCurrentPartyName([10, 11], 11, "Mate", static _ => null);

        Assert.Equal("Mate", result);
    }

    [Fact]
    public void RequesterIsNonLeaderMember_FallsBackToOwnName_WhenLeaderNameResolvesEmpty()
    {
        var result = PartyIdentityResolver.ResolveCurrentPartyName([10, 11], 11, "Mate", static _ => "");

        Assert.Equal("Mate", result);
    }

    [Fact]
    public void PartyRegistryOverload_ResolvesLeadersNameForNonLeaderMember()
    {
        var registry = new PartyRegistry();
        Assert.Equal(PartyInviteOutcome.Sent, registry.TryInvite(10, 1, 0, 11, 1, 0));
        Assert.True(registry.TryAnswer(11, true, out _, out _));

        var result = PartyIdentityResolver.ResolveCurrentPartyName(registry, 11, "Mate", id => id == 10 ? "Leader" : null);

        Assert.Equal("Leader", result);
    }

    [Fact]
    public void PartyRegistryOverload_NotPartied_ResolvesToEmpty()
    {
        var registry = new PartyRegistry();

        var result = PartyIdentityResolver.ResolveCurrentPartyName(registry, 99, "Nobody", static _ => null);

        Assert.Equal(string.Empty, result);
    }
}
