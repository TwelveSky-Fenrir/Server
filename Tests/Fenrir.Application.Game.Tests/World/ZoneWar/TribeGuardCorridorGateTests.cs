using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="TribeGuardCorridorGate" /> (<c>MyUtil::WrapCheck</c>) against a synthetic two-tribe
///     corridor -- the real sixteen-zone/hub table is not reproduced anywhere in this codebase yet (see
///     <see cref="TribeGuardCorridorCatalog" />'s own remarks).
/// </summary>
public class TribeGuardCorridorGateTests
{
    private const short HubZoneId = 100;
    private const byte OwnerTribe = 0;
    private const byte OtherTribe = 1;

    // Owner tribe 0's own chain: 1 (seg0) -> 2 (seg1) -> 3 (seg2) -> 4 (seg3, home).
    private static readonly ImmutableArray<short> Tribe0Chain = [1, 2, 3, 4];

    private static TribeGuardCorridorCatalog CreateCatalog(short? segment0Override = null)
    {
        var chain = segment0Override is { } overrideZone
            ? ImmutableArray.Create(overrideZone, (short)2, (short)3, (short)4)
            : Tribe0Chain;

        var chains = ImmutableDictionary<byte, TribeGuardCorridorChain>.Empty
            .Add(OwnerTribe, new TribeGuardCorridorChain(chain));

        return new TribeGuardCorridorCatalog(HubZoneId, chains,
            ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty);
    }

    private static TribeGuardCorridorMoveOutcome Evaluate(TribeGuardCorridorCatalog catalog,
        TribeGuardCorridorState state, byte requesterTribe, short origin, short destination,
        bool isGm = false, Func<byte, byte?>? resolveAlly = null)
    {
        return TribeGuardCorridorGate.Evaluate(catalog, state, requesterTribe, origin, destination, isGm,
            resolveAlly);
    }

    [Fact]
    public void NonCorridorDestination_IsAlwaysAllowed_RegardlessOfEverythingElse()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState(); // every segment closed

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 9999, destination: 8888);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void TheHubItselfAsDestination_IsAlwaysAllowed()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 1, destination: HubZoneId);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void GmOrAdminRank_BypassesEverything_EvenAClosedSegmentAndBadAdjacency()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState(); // segment 2 closed

        // origin 9999 is not even a valid adjacent zone -- would fail adjacency too, if evaluated.
        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 9999, destination: 3, isGm: true);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void OwningTribe_BypassesEverything_EvenAClosedSegmentAndBadAdjacency()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, requesterTribe: OwnerTribe, origin: 9999, destination: 3);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void DeclaredAllyOfTheOwningTribe_BypassesEverything()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState(); // closed

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 1, destination: 2,
            resolveAlly: owner => owner == OwnerTribe ? OtherTribe : null);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void AllyResolution_IsAlwaysQueriedAgainstTheOwningTribe_NeverTheRequester()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        byte? queriedWith = null;

        // This delegate only recognizes an alliance when queried with the OWNING tribe (0); if the gate ever
        // queried with the requester's own tribe instead, this would return null and the bypass would (wrongly)
        // not fire, matching the legacy bug class TowerFriendlyFireGate/HolyStoneTribeMatch already guard
        // against for their own analogous checks.
        byte? ResolveAlly(byte tribeId)
        {
            queriedWith = tribeId;
            return tribeId == OwnerTribe ? OtherTribe : null;
        }

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 1, destination: 2,
            resolveAlly: ResolveAlly);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
        Assert.Equal(OwnerTribe, queriedWith);
    }

    [Fact]
    public void ValidSingleStepAdvance_OpenSegment_IsAllowed()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(OwnerTribe, 1, true); // segment gating entry into zone 2 (chain[1])

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 1, destination: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void ValidSingleStepAdvance_ClosedSegment_IsRejectedSoft()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState(); // closed by default

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 1, destination: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void AdvanceFromTheHub_IntoSegmentZero_IsGatedNormally()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(OwnerTribe, 0, true);

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: HubZoneId, destination: 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void RetreatTowardTheHub_IsAlwaysAllowed_EvenWhenTheShallowerSegmentIsClosed()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState(); // segment 0 (gating zone 1) closed

        // Moving from zone 2 (segment-1 depth) back to zone 1 (segment-0 depth) -- retreating toward the hub.
        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 2, destination: 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void RetreatAllTheWayToTheHub_IsAlwaysAllowed()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 3, destination: HubZoneId);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void SkippingAnIntermediateZone_FromTheHubDirectlyPastSegmentZero_IsRejected_EvenIfOpen()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        // Segment 2 (gating entry into zone 3, the actual destination below) is OPEN -- guards defeated -- yet
        // the move must still be rejected, because adjacency is checked independently of guard state.
        state.TrySetOpen(OwnerTribe, 2, true);

        // Jumping from the hub straight into zone 3 (segment 2), skipping zone 1/2 entirely.
        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: HubZoneId, destination: 3);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void OriginFromAnEntirelyUnrelatedZone_IsRejected()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(OwnerTribe, 1, true);

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 555, destination: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void RejectionInvolvingZone37AsOrigin_IsAHardDisconnect()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState(); // closed

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 37, destination: 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedHardDisconnect, outcome);
    }

    [Fact]
    public void AdjacencyRejectionInvolvingZone37AsOrigin_IsAlsoAHardDisconnect()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(OwnerTribe, 2, true); // even with guards defeated, adjacency still fails

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 37, destination: 3);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedHardDisconnect, outcome);
    }

    [Fact]
    public void RejectionInvolvingZone37AsDestination_IsAHardDisconnect()
    {
        var catalog = CreateCatalog(segment0Override: 37); // segment 0's own destination zone is (contrived) 37
        var state = new TribeGuardCorridorState(); // closed

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: HubZoneId, destination: 37);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedHardDisconnect, outcome);
    }

    [Fact]
    public void AllowedOutcome_NeverTriggersHardDisconnect_EvenWhenZone37IsInvolved()
    {
        var catalog = CreateCatalog(segment0Override: 37);
        var state = new TribeGuardCorridorState();

        // Owning tribe's own move into its own segment-0 zone (which happens to be numbered 37) -- unconditional
        // bypass, never even reaches the hard-disconnect check.
        var outcome = Evaluate(catalog, state, requesterTribe: OwnerTribe, origin: HubZoneId, destination: 37);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void RejectionWithoutZone37Involved_IsSoftOnly()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, requesterTribe: OtherTribe, origin: 1, destination: 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }
}
