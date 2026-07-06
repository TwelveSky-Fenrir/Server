using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class GroundItemEntityTests
{
    private static GroundItemEntity Create(int dropSort, string master = "Killer", string partyName = "")
    {
        return new GroundItemEntity(1, 1u, 100, 1, 0, 0,
            0, 0, 0, master, partyName, dropSort,
            TimeSpan.Zero, 0, 0, 0);
    }

    [Fact]
    public void ExactKillerName_AlwaysClaimable_Immediately()
    {
        var item = Create(0);

        Assert.True(item.IsClaimableBy("Killer", null, TimeSpan.Zero));
    }

    [Fact]
    public void OtherPlayer_CannotClaim_BeforeFreeForAllWindow()
    {
        var item = Create(0);

        Assert.False(item.IsClaimableBy("SomeoneElse", null, TimeSpan.FromSeconds(29)));
    }

    [Fact]
    public void OtherPlayer_CanClaim_AtTheFreeForAllWindow()
    {
        var item = Create(0);

        Assert.True(item.IsClaimableBy("SomeoneElse", null, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void DropSortOne_PartyMember_CannotClaim_BeforePartyShareWindow()
    {
        var item = Create(1, partyName: "TheParty");

        Assert.False(item.IsClaimableBy("PartyMember", "TheParty", TimeSpan.FromSeconds(9)));
    }

    [Fact]
    public void DropSortOne_PartyMember_CanClaim_AtThePartyShareWindow()
    {
        var item = Create(1, partyName: "TheParty");

        Assert.True(item.IsClaimableBy("PartyMember", "TheParty", TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void DropSortOne_DifferentParty_CannotClaim_EvenAfterPartyShareWindow()
    {
        var item = Create(1, partyName: "TheParty");

        Assert.False(item.IsClaimableBy("Stranger", "SomeOtherParty", TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void DropSortZero_PartyMember_CannotClaim_EvenWithMatchingPartyName()
    {
        // DropSort 0 (solo drop) never grants the 10 s party-share window -- only DropSort 1 does.
        var item = Create(0, partyName: "TheParty");

        Assert.False(item.IsClaimableBy("PartyMember", "TheParty", TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public void IsExpired_BeforeSixtySeconds_IsFalse()
    {
        var item = Create(0);

        Assert.False(item.IsExpired(TimeSpan.FromSeconds(59.9)));
    }

    [Fact]
    public void IsExpired_AtSixtySeconds_IsTrue()
    {
        var item = Create(0);

        Assert.True(item.IsExpired(TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void EmptyOwner_ClaimableByAnyone_Immediately()
    {
        // Rule 3: an item with no recorded owner is free for anyone from the moment it lands, regardless of
        // party state or elapsed time.
        var item = Create(0, master: "");

        Assert.True(item.IsClaimableBy("AnyoneAtAll", null, TimeSpan.Zero));
    }

    [Fact]
    public void DropSortTwo_PartyMember_CannotClaim_BeforeAnyMatch()
    {
        // Rule 6 compares against Master (the owner-name field), not PartyName -- a stamped PartyName has no
        // bearing on a DropSort=ManualGroundDropSort item at all.
        var item = Create(GroundItemEntity.ManualGroundDropSort, master: "TheDropperParty", partyName: "Irrelevant");

        Assert.False(item.IsClaimableBy("Stranger", "NotTheRightParty", TimeSpan.Zero));
    }

    [Fact]
    public void DropSortTwo_MatchingPartyIdentity_ClaimableImmediately_NoExtraDelay()
    {
        // Rule 6: no extra time delay of its own beyond whatever rules 2-4 already impose.
        var item = Create(GroundItemEntity.ManualGroundDropSort, master: "TheDropperParty");

        Assert.True(item.IsClaimableBy("PartyMate", "TheDropperParty", TimeSpan.Zero));
    }

    [Fact]
    public void DropSortTwo_EmptyClaimantPartyIdentity_NeverClaimsThroughRule6()
    {
        var item = Create(GroundItemEntity.ManualGroundDropSort, master: "TheDropperParty");

        Assert.False(item.IsClaimableBy("Stranger", "", TimeSpan.Zero));
        Assert.False(item.IsClaimableBy("Stranger", null, TimeSpan.Zero));
    }

    [Fact]
    public void DropSortOne_ClaimantPartyIdentityMatchesMasterNotPartyName_StillCannotClaimThroughRule5()
    {
        // Rule 5 compares against PartyName, not Master -- a match against the wrong field must not leak
        // eligibility from rule 6's shape into rule 5's DropSort.
        var item = Create(GroundItemEntity.MonsterKillDropSort, master: "TheDropperParty", partyName: "ADifferentName");

        Assert.False(item.IsClaimableBy("PartyMate", "TheDropperParty", TimeSpan.FromSeconds(15)));
    }
}
