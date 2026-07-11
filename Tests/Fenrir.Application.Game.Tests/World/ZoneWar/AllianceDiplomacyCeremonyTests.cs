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
        var broadcaster =
            new ZoneEventBroadcaster(worldState, CreateRegistry(), NullLogger<ZoneEventBroadcaster>.Instance);
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
        var broadcaster =
            new ZoneEventBroadcaster(worldState, CreateRegistry(), NullLogger<ZoneEventBroadcaster>.Instance);

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
        worldState.SetTribePoints(0, 500);
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
        worldState.SetTribePoints(1, 200);
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
        worldState.SetAllianceOffer(2, 3, true);
        var one = new AllianceCeremonyCandidate(1, 2);
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
        worldState.SetTribePoints(1, 50);
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
        worldState.SetTribePoints(0, 100_000);
        worldState.SetTribePoints(1, 0);
        worldState.SetAllianceOffer(0, 1, true);
        cooldowns.SetCooldownUntil(0, Today.AddDays(30));
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
        ceremony.Tick(one, two, Today);

        for (var i = 0; i < AllianceDiplomacyCeremony.RejectionMessageDurationRawTicks - 1; i++)
        {
            ceremony.Tick(null, null, Today);
            Assert.Equal(AllianceCeremonyPhase.RejectionMessage, ceremony.Phase);
        }

        ceremony.Tick(null, null, Today);
        Assert.Equal(AllianceCeremonyPhase.Idle, ceremony.Phase);
    }

    [Fact]
    public void NewAllianceNegotiation_ProgressesEveryOtherRawTick_AndSkipsOddTicks()
    {
        var (ceremony, worldState, _) = CreateCeremony();
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today);

        var tick2 = ceremony.Tick(one, two, Today);
        Assert.Equal(AllianceCeremonyNotice.NewAllianceProgress, tick2.Notice);
        Assert.Equal(3, tick2.RemainingCountdown);

        var tick3 = ceremony.Tick(one, two, Today);
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
        ceremony.Tick(one, two, Today);

        var result = ceremony.Tick(one, null, Today);

        Assert.Equal(AllianceCeremonyNotice.NewAllianceAborted, result.Notice);
        Assert.Equal(one, result.RecipientOne);
        Assert.Equal(two, result.RecipientTwo);
        Assert.Equal(AllianceCeremonyPhase.Idle, ceremony.Phase);
    }

    [Fact]
    public void NewAllianceNegotiation_OddTick_DoesNotYetDetectALeaderHavingLeft()
    {
        var (ceremony, worldState, _) = CreateCeremony(10);
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today);

        var result = ceremony.Tick(one, null, Today);

        Assert.Equal(AllianceCeremonyNotice.None, result.Notice);
        Assert.Equal(AllianceCeremonyPhase.NewAllianceNegotiation, ceremony.Phase);
    }

    [Fact]
    public void NewAllianceNegotiation_CompletingTheCountdown_IsADeadEnd_NoAllianceIsEverFormed()
    {
        var (ceremony, worldState, _) = CreateCeremony(2);
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today);
        var completion = ceremony.Tick(one, two, Today);
        Assert.Equal(AllianceCeremonyPhase.NewAllianceNegotiation, ceremony.Phase);
        completion = ceremony.Tick(one, two, Today);

        Assert.Equal(AllianceCeremonyNotice.None, completion.Notice);
        Assert.Equal(AllianceCeremonyPhase.PostNegotiationCooldown, ceremony.Phase);
        Assert.Null(worldState.GetAllyOf(0));
        Assert.Null(worldState.GetAllyOf(1));
    }

    [Fact]
    public void AlreadyAlliedNegotiation_CompletingTheCountdown_DissolvesTheAlliance_AndSetsA14DayCooldownForBoth()
    {
        var (ceremony, worldState, cooldowns) = CreateCeremony(NewAllianceDuration, 2);
        worldState.SetAllianceOffer(0, 1, true);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today);
        var completion = ceremony.Tick(one, two, Today);

        Assert.Equal(AllianceCeremonyPhase.PostNegotiationCooldown, ceremony.Phase);
        Assert.Null(worldState.GetAllyOf(0));
        Assert.Null(worldState.GetAllyOf(1));
        Assert.Equal(Today.AddDays(AllianceDiplomacyCeremony.ReAllianceCooldownDays), cooldowns.GetCooldownUntil(0));
        Assert.Equal(Today.AddDays(AllianceDiplomacyCeremony.ReAllianceCooldownDays), cooldowns.GetCooldownUntil(1));
    }

    [Fact]
    public void
        PostNegotiationCooldown_HoldsForExactlyTheGameTickHourConvention_ThenReturnsToIdle_RegardlessOfWhichPathLedIntoIt()
    {
        var (ceremony, worldState, _) = CreateCeremony(2);
        worldState.SetTribePoints(0, 500);
        worldState.SetTribePoints(1, 500);
        var one = new AllianceCeremonyCandidate(1, 0);
        var two = new AllianceCeremonyCandidate(2, 1);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today);
        ceremony.Tick(one, two, Today);
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
