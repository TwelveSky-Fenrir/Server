using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class AllianceDiplomacyCeremonyTests
{
    // Arbitrary small test values -- the legacy's own negotiation-duration constant was not part of the
    // translated contract (see AllianceDiplomacyCeremony's GAP 2 remarks); these exist only to exercise the
    // countdown mechanics, not to represent real game-balance tuning.
    private const int NewAllianceDuration = 4;
    private const int AlreadyAlliedDuration = 6;

    private static readonly DateOnly Today = new(2026, 7, 6);

    private static ZoneRegistry CreateRegistry()
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize([1]);
        return registry;
    }

    private static (AllianceDiplomacyCeremony Ceremony, WorldStateService WorldState, AllianceCooldownTracker Cooldowns)
        CreateCeremony(int newAllianceDuration = NewAllianceDuration, int alreadyAlliedDuration = AlreadyAlliedDuration)
    {
        var repository = new FakeWorldStateRepository();
        var worldState = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        var cooldowns = new AllianceCooldownTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, CreateRegistry(), NullLogger<ZoneEventBroadcaster>.Instance);
        var ceremony = new AllianceDiplomacyCeremony(worldState, cooldowns, broadcaster,
            NullLogger<AllianceDiplomacyCeremony>.Instance, newAllianceDuration, alreadyAlliedDuration);

        return (ceremony, worldState, cooldowns);
    }

    [Fact]
    public async Task Constructor_RejectsNonPositiveDurations()
    {
        var repository = new FakeWorldStateRepository();
        var worldState = new WorldStateService(repository, NullLogger<WorldStateService>.Instance);
        await worldState.InitializeAsync(CancellationToken.None);
        var cooldowns = new AllianceCooldownTracker();
        var broadcaster = new ZoneEventBroadcaster(worldState, CreateRegistry(), NullLogger<ZoneEventBroadcaster>.Instance);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AllianceDiplomacyCeremony(worldState, cooldowns, broadcaster,
                NullLogger<AllianceDiplomacyCeremony>.Instance, 0, AlreadyAlliedDuration));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AllianceDiplomacyCeremony(worldState, cooldowns, broadcaster,
                NullLogger<AllianceDiplomacyCeremony>.Instance, NewAllianceDuration, -1));
    }

    [Fact]
    public void Idle_OnlyOnePostOccupied_NoTransition()
    {
        var (ceremony, _, _) = CreateCeremony();
        var one = new AllianceCeremonyCandidate(1, 0);

        var result = ceremony.Tick(one, null, Today);

        Assert.Equal(AllianceCeremonyNotice.None, result.Notice);
        Assert.Equal(AllianceCeremonyPhase.Idle, ceremony.Phase);
    }

    [Fact]
    public void Idle_BothPostsQualify_NotAllied_NoDisqualifiers_EntersNewAllianceNegotiation()
    {
        var (ceremony, worldState, _) = CreateCeremony();
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);

        var result = ceremony.Tick(one, two, Today);

        Assert.Equal(AllianceCeremonyNotice.None, result.Notice);
        Assert.Equal(AllianceCeremonyPhase.NewAllianceNegotiation, ceremony.Phase);
    }

    [Fact]
    public void Idle_DisqualifiedBySingleBiggestTribe_SendsRejectedToBoth()
    {
        var (ceremony, worldState, _) = CreateCeremony();
        worldState.SetTribePoints(0, 500); // unique biggest
        worldState.SetTribePoints(1, 200);
        worldState.SetTribePoints(2, 150);
        worldState.SetTribePoints(3, 100);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);

        var result = ceremony.Tick(one, two, Today);

        Assert.Equal(AllianceCeremonyNotice.Rejected, result.Notice);
        Assert.Equal(one, result.RecipientOne);
        Assert.Equal(two, result.RecipientTwo);
        Assert.Equal(AllianceCeremonyPhase.RejectionMessage, ceremony.Phase);
    }

    [Fact]
    public void Idle_TiedForBiggestTribe_DoesNotDisqualifyOnThatGroundAlone()
    {
        var (ceremony, worldState, _) = CreateCeremony();
        worldState.SetTribePoints(0, 200);
        worldState.SetTribePoints(1, 200); // tied with 0 -- no single biggest tribe
        worldState.SetTribePoints(2, 100);
        worldState.SetTribePoints(3, 100);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);

        var result = ceremony.Tick(one, two, Today);

        Assert.Equal(AllianceCeremonyNotice.None, result.Notice);
        Assert.Equal(AllianceCeremonyPhase.NewAllianceNegotiation, ceremony.Phase);
    }

    [Fact]
    public void Idle_DisqualifiedByExistingAlliance_SendsRejectedToBoth()
    {
        var (ceremony, worldState, _) = CreateCeremony();
        worldState.SetTribePoints(0, 150);
        worldState.SetTribePoints(1, 150);
        worldState.SetTribePoints(2, 150);
        worldState.SetTribePoints(3, 150);
        worldState.SetAllianceOffer(2, 3, isAccepted: true); // tribes 2 and 3 are already each other's ally
        var one = new AllianceCeremonyCandidate(1, 2); // tribe 2 already has an ally
        var two = new AllianceCeremonyCandidate(2, 0);

        var result = ceremony.Tick(one, two, Today);

        Assert.Equal(AllianceCeremonyNotice.Rejected, result.Notice);
        Assert.Equal(AllianceCeremonyPhase.RejectionMessage, ceremony.Phase);
    }

    [Fact]
    public void Idle_DisqualifiedByPointsBelowMinimum_SendsRejectedToBoth()
    {
        var (ceremony, worldState, _) = CreateCeremony();
        worldState.SetTribePoints(0, 50);
        worldState.SetTribePoints(1, 50); // tied -- no single biggest tribe, but both below the 100 floor
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);

        var result = ceremony.Tick(one, two, Today);

        Assert.Equal(AllianceCeremonyNotice.Rejected, result.Notice);
    }

    [Fact]
    public void Idle_DisqualifiedByReAllianceCooldown_SendsRejectedToBoth()
    {
        var (ceremony, worldState, cooldowns) = CreateCeremony();
        worldState.SetTribePoints(0, 150);
        worldState.SetTribePoints(1, 150);
        cooldowns.SetCooldownUntil(0, Today.AddDays(1));
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);

        var result = ceremony.Tick(one, two, Today);

        Assert.Equal(AllianceCeremonyNotice.Rejected, result.Notice);
    }

    [Fact]
    public void Idle_AlreadyAllied_BypassesEveryDisqualifier_EntersAlreadyAlliedNegotiation()
    {
        var (ceremony, worldState, cooldowns) = CreateCeremony();
        worldState.SetTribePoints(0, 100_000); // would otherwise be the single biggest tribe
        worldState.SetTribePoints(1, 0); // would otherwise fail the points floor
        worldState.SetAllianceOffer(0, 1, isAccepted: true);
        cooldowns.SetCooldownUntil(0, Today.AddDays(30)); // would otherwise fail the cooldown check
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);

        var result = ceremony.Tick(one, two, Today);

        Assert.Equal(AllianceCeremonyNotice.None, result.Notice);
        Assert.Equal(AllianceCeremonyPhase.AlreadyAlliedNegotiation, ceremony.Phase);
    }

    [Fact]
    public void RejectionMessage_HoldsForExactlyTheGameTickMinuteConvention_ThenReturnsToIdle()
    {
        var (ceremony, worldState, _) = CreateCeremony();
        worldState.SetTribePoints(0, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today); // rejected -> RejectionMessage, raw tick 1

        for (var i = 0; i < AllianceDiplomacyCeremony.RejectionMessageDurationRawTicks - 1; i++)
        {
            ceremony.Tick(null, null, Today);
            Assert.Equal(AllianceCeremonyPhase.RejectionMessage, ceremony.Phase);
        }

        ceremony.Tick(null, null, Today); // the 120th raw tick since entry
        Assert.Equal(AllianceCeremonyPhase.Idle, ceremony.Phase);
    }

    [Fact]
    public void NewAllianceNegotiation_ProgressesEveryOtherRawTick_AndSkipsOddTicks()
    {
        var (ceremony, worldState, _) = CreateCeremony(newAllianceDuration: 4, alreadyAlliedDuration: AlreadyAlliedDuration);
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today); // raw tick 1: enters NewAllianceNegotiation, countdown = 4

        var tick2 = ceremony.Tick(one, two, Today); // raw tick 2 (even) -- validated, countdown -> 3
        Assert.Equal(AllianceCeremonyNotice.NewAllianceProgress, tick2.Notice);
        Assert.Equal(3, tick2.RemainingCountdown);

        var tick3 = ceremony.Tick(one, two, Today); // raw tick 3 (odd) -- skipped
        Assert.Equal(AllianceCeremonyNotice.None, tick3.Notice);
        Assert.Equal(AllianceCeremonyPhase.NewAllianceNegotiation, ceremony.Phase);
    }

    [Fact]
    public void NewAllianceNegotiation_LeaderIdentityChanges_AbortsImmediately_OnTheNextValidatedTick()
    {
        var (ceremony, worldState, _) = CreateCeremony();
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today); // raw tick 1: enters negotiation

        // Raw tick 2 (even/validated): post two's leader is gone.
        var result = ceremony.Tick(one, null, Today);

        Assert.Equal(AllianceCeremonyNotice.NewAllianceAborted, result.Notice);
        Assert.Equal(one, result.RecipientOne);
        Assert.Equal(two, result.RecipientTwo);
        Assert.Equal(AllianceCeremonyPhase.Idle, ceremony.Phase);
    }

    [Fact]
    public void NewAllianceNegotiation_OddTick_DoesNotYetDetectALeaderHavingLeft()
    {
        var (ceremony, worldState, _) = CreateCeremony(newAllianceDuration: 10, alreadyAlliedDuration: AlreadyAlliedDuration);
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today); // raw tick 1
        ceremony.Tick(one, two, Today); // raw tick 2 (even, validated, countdown -> 9)

        // Raw tick 3 (odd): leader two vanished, but parity means this is not checked yet.
        var result = ceremony.Tick(one, null, Today);

        Assert.Equal(AllianceCeremonyNotice.None, result.Notice);
        Assert.Equal(AllianceCeremonyPhase.NewAllianceNegotiation, ceremony.Phase);
    }

    [Fact]
    public void NewAllianceNegotiation_CompletingTheCountdown_IsADeadEnd_NoAllianceIsEverFormed()
    {
        var (ceremony, worldState, _) = CreateCeremony(newAllianceDuration: 2, alreadyAlliedDuration: AlreadyAlliedDuration);
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today); // raw tick 1: enters negotiation, countdown = 2
        ceremony.Tick(one, two, Today); // raw tick 2 (even): countdown -> 1
        var completion = ceremony.Tick(one, two, Today); // raw tick 3 (odd): skipped
        Assert.Equal(AllianceCeremonyPhase.NewAllianceNegotiation, ceremony.Phase);
        completion = ceremony.Tick(one, two, Today); // raw tick 4 (even): countdown -> 0, completes

        Assert.Equal(AllianceCeremonyNotice.None, completion.Notice);
        Assert.Equal(AllianceCeremonyPhase.PostNegotiationCooldown, ceremony.Phase);
        Assert.Null(worldState.GetAllyOf(0)); // no alliance was ever created
        Assert.Null(worldState.GetAllyOf(1));
    }

    [Fact]
    public void AlreadyAlliedNegotiation_CompletingTheCountdown_DissolvesTheAlliance_AndSetsA14DayCooldownForBoth()
    {
        var (ceremony, worldState, cooldowns) = CreateCeremony(newAllianceDuration: NewAllianceDuration, alreadyAlliedDuration: 2);
        worldState.SetAllianceOffer(0, 1, isAccepted: true);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today); // raw tick 1: already allied -> enters AlreadyAlliedNegotiation, countdown = 2
        ceremony.Tick(one, two, Today); // raw tick 2 (even): countdown -> 1
        ceremony.Tick(one, two, Today); // raw tick 3 (odd): skipped
        var completion = ceremony.Tick(one, two, Today); // raw tick 4 (even): countdown -> 0, completes

        Assert.Equal(AllianceCeremonyPhase.PostNegotiationCooldown, ceremony.Phase);
        Assert.Null(worldState.GetAllyOf(0));
        Assert.Null(worldState.GetAllyOf(1));
        Assert.Equal(Today.AddDays(AllianceDiplomacyCeremony.ReAllianceCooldownDays), cooldowns.GetCooldownUntil(0));
        Assert.Equal(Today.AddDays(AllianceDiplomacyCeremony.ReAllianceCooldownDays), cooldowns.GetCooldownUntil(1));
    }

    [Fact]
    public void PostNegotiationCooldown_HoldsForExactlyTheGameTickHourConvention_ThenReturnsToIdle_RegardlessOfWhichPathLedIntoIt()
    {
        var (ceremony, worldState, _) = CreateCeremony(newAllianceDuration: 2, alreadyAlliedDuration: AlreadyAlliedDuration);
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today); // completes -> PostNegotiationCooldown
        Assert.Equal(AllianceCeremonyPhase.PostNegotiationCooldown, ceremony.Phase);

        for (var i = 0; i < AllianceDiplomacyCeremony.PostNegotiationCooldownDurationRawTicks - 1; i++)
        {
            ceremony.Tick(null, null, Today);
            Assert.Equal(AllianceCeremonyPhase.PostNegotiationCooldown, ceremony.Phase);
        }

        ceremony.Tick(null, null, Today);
        Assert.Equal(AllianceCeremonyPhase.Idle, ceremony.Phase);
    }
}
