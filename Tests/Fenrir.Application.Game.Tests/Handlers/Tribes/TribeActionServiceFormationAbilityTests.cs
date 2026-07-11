using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_WORK_SEND tSort 5 (<see cref="TribeActionService.ValidateTribeSkill" />) -- the declaration
///     path for B10 branch A (tribe Formation ability). Kept in its own file rather than added to
///     <c>TribeActionServiceTests</c> to avoid touching a file a concurrent pass may also be editing (same
///     rationale as <c>WorldStateServiceFormationAbilityTests</c>, which covers the
///     <see cref="WorldStateService.SetTribeFormationAbility" />/<see cref="WorldStateService.GetTribeFormationAbility" />
///     primitives themselves -- this file only covers the one caller that arms them).
///     <para>
///         All five eligibility gates named by <see cref="WorldStateService.SetTribeFormationAbility" />'s own
///         remarks are now enforced by <see cref="TribeActionService.ValidateTribeSkill" /> and covered here:
///         Force Leader role and payload shape/range (the two gates this file originally covered), plus gates
///         (a)-(d) -- four-tribe point floor, strict-lowest-tribe tie-break, twenty-percent share, and Tribe
///         Symbol Battle active. The pure numeric edges of gates (a)-(c) (exact floor/share boundaries, the
///         tie-break rule itself) are covered in isolation by
///         <c>TribeFormationAbilityEligibilityTests</c> against <see cref="TribeFormationAbilityEligibility" />
///         directly -- this file instead covers gate (d) (a <see cref="WorldStateService.World" /> flag read,
///         not a pure function those tests can exercise) and the full five-gate END-TO-END composition: each
///         gate failing alone must abort without arming anything, and all five passing must reach
///         <see cref="WorldStateService.SetTribeFormationAbility" />.
///     </para>
/// </summary>
public class TribeActionServiceFormationAbilityTests
{
    private const int CharacterId = 10;

    /// <summary>
    ///     Default per-tribe point totals that make every one of gates (a)-(c) pass regardless of which tribe
    ///     is chosen as <paramref name="requesterTribe" />: the requester's own tribe sits at 101 (one over
    ///     <see cref="TribeFormationAbilityEligibility.PointFloor" />, and strictly below every other tribe's
    ///     1000, so it is always the sole lowest-point tribe and its 101-of-3101 share is comfortably under
    ///     <see cref="TribeFormationAbilityEligibility.SharePercentThreshold" />).
    /// </summary>
    private static int[] DefaultPassingTribePoints(byte requesterTribe)
    {
        var points = new int[4];
        for (byte i = 0; i < 4; i++)
            points[i] = i == requesterTribe ? TribeFormationAbilityEligibility.PointFloor + 1 : 1000;

        return points;
    }

    private static (PlayerRuntimeState State, WorldStateService WorldState, TribeActionService Service) Setup(
        byte tribe = 2, byte tribeRole = 1, int[]? tribePoints = null, bool symbolBattleActive = true)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(CharacterId);
        session.MarkTicketConsumed(1, CharacterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, zone.MapId, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.TryGetPlayer(CharacterId, out var state);
        state!.TribeRole = tribeRole;

        var worldState = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        worldState.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Gates (a)-(d): seed a baseline where every gate passes by default (see DefaultPassingTribePoints),
        // so tests unrelated to a specific gate don't need to think about it -- a test targeting one gate's
        // failure passes its own tribePoints/symbolBattleActive override instead.
        var points = tribePoints ?? DefaultPassingTribePoints(tribe);
        for (byte i = 0; i < 4; i++)
            worldState.SetTribePoints(i, points[i]);

        if (symbolBattleActive)
            worldState.StartTribeSymbolBattle();

        var characters = new FakeCharacterRepository();
        var options = Options.Create(ZoneTestKit.Options());
        var registry = new ZoneRegistry(options, new MovementRules(options), new DirtyTracker<int>(),
            NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize([1]);

        var service = new TribeActionService(registry, new FakeTribeRepository(), characters,
            ZoneTestKit.EmptyWorldData(), worldState, NullLogger<TribeActionService>.Instance);

        return (state, worldState, service);
    }

    private static byte[] SkillPayload(int tribeSkillSort)
    {
        var payload = new TribeWorkSkillPayload { TribeSkillSort = tribeSkillSort };
        var buffer = new byte[TribeWorkSkillPayload.WireSize];
        payload.Write(buffer);
        return buffer;
    }

    [Fact]
    public void ForceLeader_ValidCode_ArmsWorldStateFormationAbility_AndReturnsOk()
    {
        var (state, worldState, service) = Setup(tribe: 2, tribeRole: 1);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.False(outcome.Aborted);
        Assert.Equal(0, outcome.Result);
        Assert.Equal(3, worldState.GetTribeFormationAbility(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ForceLeader_EveryInRangeCode_IsThreadedThroughVerbatim(int code)
    {
        var (state, worldState, service) = Setup(tribe: 1, tribeRole: 1);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(code));

        Assert.False(outcome.Aborted);
        Assert.Equal(code, worldState.GetTribeFormationAbility(1));
    }

    [Fact]
    public void ArmsOnlyTheRequestersOwnTribeSlot_OtherTribesUntouched()
    {
        var (state, worldState, service) = Setup(tribe: 2, tribeRole: 1);

        service.ValidateTribeSkill(state, SkillPayload(4));

        Assert.Equal(0, worldState.GetTribeFormationAbility(0));
        Assert.Equal(0, worldState.GetTribeFormationAbility(1));
        Assert.Equal(4, worldState.GetTribeFormationAbility(2));
        Assert.Equal(0, worldState.GetTribeFormationAbility(3));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public void NotForceLeader_Aborts_NeverArmsFormationAbility(byte tribeRole)
    {
        var (state, worldState, service) = Setup(tribe: 2, tribeRole: tribeRole);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.True(outcome.Aborted);
        Assert.Equal(0, worldState.GetTribeFormationAbility(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(100)]
    public void OutOfRangePayload_Aborts_NeverArmsFormationAbility(int outOfRangeSort)
    {
        var (state, worldState, service) = Setup(tribe: 2, tribeRole: 1);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(outOfRangeSort));

        Assert.True(outcome.Aborted);
        Assert.Equal(0, worldState.GetTribeFormationAbility(2));
    }

    [Fact]
    public void MalformedPayload_TooShort_Aborts_NeverArmsFormationAbility()
    {
        var (state, worldState, service) = Setup(tribe: 2, tribeRole: 1);

        var outcome = service.ValidateTribeSkill(state, new byte[3]);

        Assert.True(outcome.Aborted);
        Assert.Equal(0, worldState.GetTribeFormationAbility(2));
    }

    [Fact]
    public void ANewDeclaration_FullyReplacesThePreviousCode_NeverStacks()
    {
        var (state, worldState, service) = Setup(tribe: 3, tribeRole: 1);

        service.ValidateTribeSkill(state, SkillPayload(1));
        service.ValidateTribeSkill(state, SkillPayload(2));

        Assert.Equal(2, worldState.GetTribeFormationAbility(3));
    }

    [Fact]
    public void FloorGate_OneTribeAtExactFloor_Aborts_NeverArmsFormationAbility()
    {
        // Tribe 0 sits exactly at the floor (100) -- gate (a) is a GLOBAL floor across all four tribes, so
        // this fails even though the requester (tribe 2) individually clears every other gate.
        var points = DefaultPassingTribePoints(2);
        points[0] = TribeFormationAbilityEligibility.PointFloor;
        var (state, worldState, service) = Setup(tribe: 2, tribePoints: points);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.True(outcome.Aborted);
        Assert.Equal(0, worldState.GetTribeFormationAbility(2));
    }

    [Fact]
    public void LowestTribeGate_RequesterTribeIsNotTheLowest_Aborts_NeverArmsFormationAbility()
    {
        // Tribe 1 holds the true lowest total (101); tribe 2 (the requester) sits well above it -- gate (b)
        // must reject even though every tribe individually clears the floor from gate (a).
        var (state, worldState, service) = Setup(tribe: 2, tribePoints: [1000, 101, 1000, 1000]);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.True(outcome.Aborted);
        Assert.Equal(0, worldState.GetTribeFormationAbility(2));
    }

    [Fact]
    public void ShareGate_RequesterShareAtOrAboveTwentyPercent_Aborts_NeverArmsFormationAbility()
    {
        // Requester tribe 0 is still the strict lowest (150 < 200 for every other tribe), so gates (a)-(b)
        // pass, but its share of the combined 750 total is exactly 20% -- at-or-above the threshold, so gate
        // (c) must still abort.
        var (state, worldState, service) = Setup(tribe: 0, tribePoints: [150, 200, 200, 200]);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.True(outcome.Aborted);
        Assert.Equal(0, worldState.GetTribeFormationAbility(0));
    }

    [Fact]
    public void SymbolBattleGate_Inactive_Aborts_NeverArmsFormationAbility()
    {
        // Gates (a)-(c) pass (the default seeded points), but the Tribe Symbol Battle world event was never
        // started -- gate (d) must reject.
        var (state, worldState, service) = Setup(tribe: 2, symbolBattleActive: false);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.True(outcome.Aborted);
        Assert.Equal(0, worldState.GetTribeFormationAbility(2));
    }

    [Fact]
    public void SymbolBattleGate_Active_PassesAlongsideEveryOtherGate()
    {
        var (state, worldState, service) = Setup(tribe: 2, symbolBattleActive: true);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.False(outcome.Aborted);
        Assert.Equal(3, worldState.GetTribeFormationAbility(2));
    }

    [Fact]
    public void AllFiveGates_Pass_ReachesSetTribeFormationAbility()
    {
        // Explicit, fully-worked positive case for every one of the five gates at once: Force Leader role
        // (tribeRole 1), payload shape/range (code 3, within 0-4), all four tribes above the floor
        // (101/1000/1000/1000), the requester (tribe 0) is the strict single lowest, its 101-of-3101 share is
        // well under twenty percent, and the Tribe Symbol Battle world event is active.
        var (state, worldState, service) = Setup(tribe: 0, tribeRole: 1,
            tribePoints: [TribeFormationAbilityEligibility.PointFloor + 1, 1000, 1000, 1000],
            symbolBattleActive: true);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.False(outcome.Aborted);
        Assert.Equal(3, worldState.GetTribeFormationAbility(0));
    }
}
