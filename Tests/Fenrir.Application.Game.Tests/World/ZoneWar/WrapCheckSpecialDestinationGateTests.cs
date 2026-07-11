using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="WrapCheckSpecialDestinationGate" /> -- the win-zone-038/rebirth-exact/instanced groups,
///     plus its fall-through composition with <see cref="TribeGuardCorridorGate" /> for everything else.
/// </summary>
public class WrapCheckSpecialDestinationGateTests
{
    private const short WinZone038Destination = 39;
    private const short RebirthGatedDestination = 241; // requires exactly rebirth 1
    private const short InstancedDestination = 325; // requires rebirth >= 1

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
        var outcome = Evaluate(1, 9999, WinZone038Destination, isGm: true, zone38Winner: 0);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void WinZone038Group_MoverIsTheCurrentHolder_IsAllowed()
    {
        var outcome = Evaluate(requesterTribe: 2, origin: 1, destination: WinZone038Destination, zone38Winner: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void WinZone038Group_MoverIsTheHoldersDeclaredAlly_IsAllowed()
    {
        var outcome = Evaluate(requesterTribe: 3, origin: 1, destination: WinZone038Destination, zone38Winner: 2,
            resolveAlly: holder => holder == 2 ? (byte)3 : null);

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

        Evaluate(requesterTribe: 3, origin: 1, destination: WinZone038Destination, zone38Winner: 2,
            resolveAlly: ResolveAlly);

        Assert.Equal((byte)2, queriedWith);
    }

    [Fact]
    public void WinZone038Group_MoverIsNeitherHolderNorAlly_IsRejectedSoft()
    {
        var outcome = Evaluate(requesterTribe: 1, origin: 5, destination: WinZone038Destination, zone38Winner: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void WinZone038Group_NobodyHasEverCapturedIt_NeverMatchesAnyTribe()
    {
        var outcome = Evaluate(requesterTribe: 0, origin: 5, destination: WinZone038Destination, zone38Winner: null);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void RebirthGatedGroup_ExactMatch_IsAllowed()
    {
        var outcome = Evaluate(requesterTribe: 0, origin: 5, destination: RebirthGatedDestination, rebirth: 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(12)]
    public void RebirthGatedGroup_AnyOtherRebirthCount_IsRejectedSoft(int rebirth)
    {
        var outcome = Evaluate(requesterTribe: 0, origin: 5, destination: RebirthGatedDestination, rebirth: rebirth);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void InstancedGroup_RebirthAtLeastOne_IsAllowed()
    {
        var outcome = Evaluate(requesterTribe: 0, origin: 5, destination: InstancedDestination, rebirth: 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void InstancedGroup_NeverRebirthed_IsRejectedSoft()
    {
        var outcome = Evaluate(requesterTribe: 0, origin: 5, destination: InstancedDestination, rebirth: 0);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Theory]
    [InlineData(WinZone038Destination)]
    [InlineData(RebirthGatedDestination)]
    [InlineData(InstancedDestination)]
    public void EveryGroupsRejection_InvolvingZone37AsOrigin_IsAHardDisconnect(short destination)
    {
        var outcome = Evaluate(requesterTribe: 1, origin: 37, destination: destination, rebirth: 0,
            zone38Winner: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedHardDisconnect, outcome);
    }

    [Fact]
    public void NotASpecialDestination_FallsThroughToTheCorridorGatesOwnDefaultAllow()
    {
        // 9999 belongs to none of the four groups (three special + tribe-corridor) -- TribeGuardCorridorGate's
        // own unconditional default-allow applies.
        var outcome = Evaluate(requesterTribe: 1, origin: 1, destination: 9999);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void NotASpecialDestination_FallsThroughToTheRealTribeCorridorGate_AndIsStillGated()
    {
        var catalog = TribeGuardCorridorCatalogFactory.BuildLive();

        // Zone 1 is tribe 0's own town (a real tribe-corridor destination, not one of the three special
        // groups) -- an enemy (tribe 1) advancing into it with every segment closed must still be rejected.
        var outcome = Evaluate(requesterTribe: 1, origin: 2, destination: 1, corridorCatalog: catalog);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }
}
