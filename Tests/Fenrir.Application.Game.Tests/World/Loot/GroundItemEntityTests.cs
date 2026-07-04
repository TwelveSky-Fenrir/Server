using Fenrir.Application.Game.World.Loot;

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
}
