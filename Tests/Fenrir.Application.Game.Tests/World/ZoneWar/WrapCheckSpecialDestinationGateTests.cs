using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class WrapCheckSpecialDestinationGateTests
{
    private const short WinZone038Destination = 39;
    private const short RebirthGatedDestination = 241;
    private const short InstancedDestination = 325;

    private static TribeGuardCorridorMoveOutcome Evaluate(
        byte requesterTribe,
        short origin,
        short destination,
        bool isGm = false,
        int rebirth = 0,
        byte? zone38Winner = null,
        Func<byte, byte?>? resolveAlly = null,
        TribeGuardCorridorCatalog? corridorCatalog = null)
    {
        return WrapCheckSpecialDestinationGate.Evaluate(
            corridorCatalog ?? TribeGuardCorridorCatalog.Empty,
            new TribeGuardCorridorState(),
            requesterTribe,
            origin,
            destination,
            isGm,
            rebirth,
            zone38Winner,
            resolveAlly);
    }

    [Fact]
    public void GmOrAdminRank_BypassesEveryGroup()
    {
        var outcome = Evaluate(1, 9999, WinZone038Destination, true, zone38Winner: 0);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void WinZone038Group_MoverIsTheCurrentHolder_IsAllowed()
    {
        var outcome = Evaluate(2, 1, WinZone038Destination, zone38Winner: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void WinZone038Group_MoverIsTheHoldersDeclaredAlly_IsAllowed()
    {
        var outcome = Evaluate(3, 1, WinZone038Destination, zone38Winner: 2,
            resolveAlly: holder => holder == 2 ? 3 : null);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void WinZone038Group_AllyResolvedAgainstTheHolder_NeverTheRequester()
    {
        byte? queriedWith = null;

        byte? ResolveAlly(byte tribeId)
        {
            queriedWith = tribeId;
            return null;
        }

        Evaluate(3, 1, WinZone038Destination, zone38Winner: 2,
            resolveAlly: ResolveAlly);

        Assert.Equal((byte)2, queriedWith);
    }

    [Fact]
    public void WinZone038Group_MoverIsNeitherHolderNorAlly_IsRejectedSoft()
    {
        var outcome = Evaluate(1, 5, WinZone038Destination, zone38Winner: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void WinZone038Group_NobodyHasEverCapturedIt_NeverMatchesAnyTribe()
    {
        var outcome = Evaluate(0, 5, WinZone038Destination, zone38Winner: null);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void RebirthGatedGroup_ExactMatch_IsAllowed()
    {
        var outcome = Evaluate(0, 5, RebirthGatedDestination, rebirth: 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(12)]
    public void RebirthGatedGroup_AnyOtherRebirthCount_IsRejectedSoft(int rebirth)
    {
        var outcome = Evaluate(0, 5, RebirthGatedDestination, rebirth: rebirth);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void InstancedGroup_RebirthAtLeastOne_IsAllowed()
    {
        var outcome = Evaluate(0, 5, InstancedDestination, rebirth: 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void InstancedGroup_NeverRebirthed_IsRejectedSoft()
    {
        var outcome = Evaluate(0, 5, InstancedDestination, rebirth: 0);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Theory]
    [InlineData(WinZone038Destination)]
    [InlineData(RebirthGatedDestination)]
    [InlineData(InstancedDestination)]
    public void EveryGroupsRejection_InvolvingZone37AsOrigin_IsAHardDisconnect(short destination)
    {
        var outcome = Evaluate(1, 37, destination, rebirth: 0,
            zone38Winner: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedHardDisconnect, outcome);
    }

    [Fact]
    public void NotASpecialDestination_FallsThroughToTheCorridorGatesOwnDefaultAllow()
    {
        var outcome = Evaluate(1, 1, 9999);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void NotASpecialDestination_FallsThroughToTheRealTribeCorridorGate_AndIsStillGated()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        var outcome = Evaluate(1, 2, 1, corridorCatalog: catalog);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }
}
