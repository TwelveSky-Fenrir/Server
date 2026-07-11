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

public class TribeActionServiceFormationAbilityTests
{
    private const int CharacterId = 10;

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
        var (state, worldState, service) = Setup(tribe: 2, tribePoints: [1000, 101, 1000, 1000]);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.True(outcome.Aborted);
        Assert.Equal(0, worldState.GetTribeFormationAbility(2));
    }

    [Fact]
    public void ShareGate_RequesterShareAtOrAboveTwentyPercent_Aborts_NeverArmsFormationAbility()
    {
        var (state, worldState, service) = Setup(tribe: 0, tribePoints: [150, 200, 200, 200]);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.True(outcome.Aborted);
        Assert.Equal(0, worldState.GetTribeFormationAbility(0));
    }

    [Fact]
    public void SymbolBattleGate_Inactive_Aborts_NeverArmsFormationAbility()
    {
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
        var (state, worldState, service) = Setup(tribe: 0, tribeRole: 1,
            tribePoints: [TribeFormationAbilityEligibility.PointFloor + 1, 1000, 1000, 1000],
            symbolBattleActive: true);

        var outcome = service.ValidateTribeSkill(state, SkillPayload(3));

        Assert.False(outcome.Aborted);
        Assert.Equal(3, worldState.GetTribeFormationAbility(0));
    }
}
