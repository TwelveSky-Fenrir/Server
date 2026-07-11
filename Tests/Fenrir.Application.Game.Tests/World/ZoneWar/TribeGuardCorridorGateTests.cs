using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeGuardCorridorGateTests
{
    private const short HubZoneId = 100;
    private const byte OwnerTribe = 0;
    private const byte OtherTribe = 1;

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
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, 9999, 8888);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void TheHubItselfAsDestination_IsAlwaysAllowed()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, 1, HubZoneId);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void GmOrAdminRank_BypassesEverything_EvenAClosedSegmentAndBadAdjacency()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, 9999, 3, true);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void OwningTribe_BypassesEverything_EvenAClosedSegmentAndBadAdjacency()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OwnerTribe, 9999, 3);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void DeclaredAllyOfTheOwningTribe_BypassesEverything()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, 1, 2,
            resolveAlly: owner => owner == OwnerTribe ? OtherTribe : null);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void AllyResolution_IsAlwaysQueriedAgainstTheOwningTribe_NeverTheRequester()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        byte? queriedWith = null;

        byte? ResolveAlly(byte tribeId)
        {
            queriedWith = tribeId;
            return tribeId == OwnerTribe ? OtherTribe : null;
        }

        var outcome = Evaluate(catalog, state, OtherTribe, 1, 2,
            resolveAlly: ResolveAlly);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
        Assert.Equal(OwnerTribe, queriedWith);
    }

    [Fact]
    public void ValidSingleStepAdvance_OpenSegment_IsAllowed()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(OwnerTribe, 1, true);

        var outcome = Evaluate(catalog, state, OtherTribe, 1, 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void ValidSingleStepAdvance_ClosedSegment_IsRejectedSoft()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, 1, 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void AdvanceFromTheHub_IntoSegmentZero_IsGatedNormally()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(OwnerTribe, 0, true);

        var outcome = Evaluate(catalog, state, OtherTribe, HubZoneId, 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void RetreatTowardTheHub_IsAlwaysAllowed_EvenWhenTheShallowerSegmentIsClosed()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, 2, 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void RetreatAllTheWayToTheHub_IsAlwaysAllowed()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, 3, HubZoneId);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void SkippingAnIntermediateZone_FromTheHubDirectlyPastSegmentZero_IsRejected_EvenIfOpen()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(OwnerTribe, 2, true);

        var outcome = Evaluate(catalog, state, OtherTribe, HubZoneId, 3);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void OriginFromAnEntirelyUnrelatedZone_IsRejected()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(OwnerTribe, 1, true);

        var outcome = Evaluate(catalog, state, OtherTribe, 555, 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }

    [Fact]
    public void RejectionInvolvingZone37AsOrigin_IsAHardDisconnect()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, 37, 1);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedHardDisconnect, outcome);
    }

    [Fact]
    public void AdjacencyRejectionInvolvingZone37AsOrigin_IsAlsoAHardDisconnect()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(OwnerTribe, 2, true);

        var outcome = Evaluate(catalog, state, OtherTribe, 37, 3);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedHardDisconnect, outcome);
    }

    [Fact]
    public void RejectionInvolvingZone37AsDestination_IsAHardDisconnect()
    {
        var catalog = CreateCatalog(37);
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, HubZoneId, 37);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedHardDisconnect, outcome);
    }

    [Fact]
    public void AllowedOutcome_NeverTriggersHardDisconnect_EvenWhenZone37IsInvolved()
    {
        var catalog = CreateCatalog(37);
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OwnerTribe, HubZoneId, 37);

        Assert.Equal(TribeGuardCorridorMoveOutcome.Allowed, outcome);
    }

    [Fact]
    public void RejectionWithoutZone37Involved_IsSoftOnly()
    {
        var catalog = CreateCatalog();
        var state = new TribeGuardCorridorState();

        var outcome = Evaluate(catalog, state, OtherTribe, 1, 2);

        Assert.Equal(TribeGuardCorridorMoveOutcome.RejectedSoft, outcome);
    }
}
