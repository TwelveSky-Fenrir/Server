using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

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
        var allowed = TowerFriendlyFireGate.CanAttackGuardian(
            1, 0, true, 3);

        Assert.True(allowed);
    }

    [Fact]
    public void PassingAllyOfAttackerInsteadOfOwner_WouldNeverExemptAnyone_DemonstratingWhyTheFixMatters()
    {
        const byte attackerTribe = 2;
        const byte owningTribe = 0;

        var buggyAllyOfAttacker = owningTribe;
        var allowedUnderTheBug = TowerFriendlyFireGate.CanAttackGuardian(
            attackerTribe, owningTribe, true, buggyAllyOfAttacker);
        Assert.True(allowedUnderTheBug);

        var correctAllyOfOwner = attackerTribe;
        var allowedUnderTheFix = TowerFriendlyFireGate.CanAttackGuardian(
            attackerTribe, owningTribe, true, correctAllyOfOwner);
        Assert.False(allowedUnderTheFix);
    }
}
