using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

/// <summary>
///     <see cref="TowerFriendlyFireGate.CanAttackGuardian" /> -- the avatar-vs-tower-guardian attack
///     authorization gate, including the legacy friendly-fire bug this method exists to fix (see its own
///     remarks for the exact citation).
/// </summary>
public class TowerFriendlyFireGateTests
{
    [Fact]
    public void NotATowerZone_OwningTribeNull_IsAlwaysRejected()
    {
        var allowed = TowerFriendlyFireGate.CanAttackGuardian(
            1, null, true, null);

        Assert.False(allowed);
    }

    [Fact]
    public void TowerNotActivelyBuilt_IsRejected_RegardlessOfTribe()
    {
        var allowed = TowerFriendlyFireGate.CanAttackGuardian(
            1, 0, false, null);

        Assert.False(allowed);
    }

    [Fact]
    public void AttackerIsTheOwningTribe_IsRejected_SelfTribeProtection()
    {
        var allowed = TowerFriendlyFireGate.CanAttackGuardian(
            0, 0, true, null);

        Assert.False(allowed);
    }

    [Fact]
    public void AttackerTribeIsAlliedWithTheOwningTribe_IsRejected_TheFriendlyFireFix()
    {
        // Owner is tribe 0, tribe 0 is allied with tribe 2 -- an attacker from tribe 2 must be blocked.
        var allowed = TowerFriendlyFireGate.CanAttackGuardian(
            2, 0, true, 2);

        Assert.False(allowed);
    }

    [Fact]
    public void UnrelatedTribe_NoAllianceInvolved_IsAllowed()
    {
        var allowed = TowerFriendlyFireGate.CanAttackGuardian(
            1, 0, true, null);

        Assert.True(allowed);
    }

    [Fact]
    public void OwningTribeHasADifferentAlly_AttackerUnaffected_IsAllowed()
    {
        // Owner (tribe 0) is allied with tribe 3, but the attacker is tribe 1 -- unrelated, must be allowed.
        var allowed = TowerFriendlyFireGate.CanAttackGuardian(
            1, 0, true, 3);

        Assert.True(allowed);
    }

    /// <summary>
    ///     Locks in the actual fix: the legacy bug resolves the ally of the ATTACKER's own tribe and compares
    ///     it back against the attacker, which (per <c>ReturnAlliance</c>'s own contract -- an ally lookup
    ///     never returns the tribe passed to it) can never be true, so the exemption never fires there. This
    ///     gate takes the ally of the OWNER instead; feeding it "ally of attacker" would reproduce exactly the
    ///     legacy hole this behavior exists to close.
    /// </summary>
    [Fact]
    public void PassingAllyOfAttackerInsteadOfOwner_WouldNeverExemptAnyone_DemonstratingWhyTheFixMatters()
    {
        const byte attackerTribe = 2;
        const byte owningTribe = 0;

        // Ally of the ATTACKER's own tribe (2) is tribe 0 -- but ReturnAlliance never returns the tribe passed
        // in, so "ally of attacker" can never equal the attacker itself; feeding that value here reproduces
        // the legacy bug and the exemption silently never fires.
        var buggyAllyOfAttacker = owningTribe;
        var allowedUnderTheBug = TowerFriendlyFireGate.CanAttackGuardian(
            attackerTribe, owningTribe, true, buggyAllyOfAttacker);
        Assert.True(allowedUnderTheBug); // the bug: never blocked, because ally-of-attacker != attacker

        // Ally of the OWNER (0) is tribe 2 -- the correct input -- correctly blocks the attacker.
        var correctAllyOfOwner = attackerTribe;
        var allowedUnderTheFix = TowerFriendlyFireGate.CanAttackGuardian(
            attackerTribe, owningTribe, true, correctAllyOfOwner);
        Assert.False(allowedUnderTheFix);
    }
}
