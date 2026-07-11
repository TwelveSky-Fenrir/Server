using System.Buffers.Binary;
using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribeActionServiceTests
{
    private const int CharacterId = 10;
    private const int NeighborId = 20;

    private const int MaxHighLevelExp = 1_481_117_817;

    private static int RebirthFrame => FrameWriter.FrameSizeOf<TribeActionResponse>();
    private static int StateFlagFrame => FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId, short level = 145, short level2 = 12, int exp2 = MaxHighLevelExp,
        int contributionPoints = 10_000, int rebirthCount = 0)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId, level: level)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        state!.Level2 = level2;
        state.Exp2 = exp2;
        state.ContributionPoints = contributionPoints;
        state.RebirthCount = rebirthCount;

        return (session, pipe, state);
    }

    private static TribeActionService CreateService(FakeCharacterRepository? characters = null,
        ILogger<TribeActionService>? logger = null, WorldStateService? worldState = null)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize([1]);

        var levels = new Dictionary<short, LevelRowDto> { [145] = WorldDataTestRows.Level(145) }
            .ToFrozenDictionary();

        return new TribeActionService(registry, new FakeTribeRepository(), characters ?? new FakeCharacterRepository(),
            ZoneTestKit.EmptyWorldData(levelsByLevel: levels), worldState ?? CreateInitializedWorldState(),
            logger ?? NullLogger<TribeActionService>.Instance);
    }

    private static WorldStateService CreateInitializedWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    private static TribeActionRequest RebirthRequest()
    {
        return new TribeActionRequest { Sort = 11, Data = new byte[100] };
    }

    private static void Respond(ZoneClientSession session, TribeActionRequest packet, TribeActionOutcome outcome)
    {
        if (outcome.Aborted)
        {
            session.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new TribeActionResponse { Result = outcome.Result, Sort = packet.Sort, Data = packet.Data });
    }

    [Fact]
    public async Task HaloEnchant_FirstAttempt_PassesTheReentryGuard_ButFailsTheSeparateHaloCapCheck()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId, contributionPoints: 1_000);
        state.Halo = 96;
        state.LastHaloEnchantAttemptUtc = DateTime.UtcNow - TimeSpan.FromSeconds(1);
        var logger = new CapturingLogger<TribeActionService>();
        var service = CreateService(logger: logger);

        var outcome = await service.HaloEnchantAsync(zone, state, CharacterId, CancellationToken.None);

        Assert.True(outcome.Aborted);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("same-tick repeat request"));
        Assert.Equal(96, state.Halo);
        Assert.Equal(1_000, state.ContributionPoints);
    }

    [Fact]
    public async Task HaloEnchant_SecondAttemptWithinSameLegacyTick_IsRejectedByTheReentryGuard()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, CharacterId, contributionPoints: 1_000);
        state.Halo = 96;
        state.LastHaloEnchantAttemptUtc = DateTime.UtcNow - TimeSpan.FromSeconds(1);
        var logger = new CapturingLogger<TribeActionService>();
        var service = CreateService(logger: logger);

        var first = await service.HaloEnchantAsync(zone, state, CharacterId, CancellationToken.None);
        Assert.True(first.Aborted);
        var stampedAfterFirst = state.LastHaloEnchantAttemptUtc;

        var second = await service.HaloEnchantAsync(zone, state, CharacterId, CancellationToken.None);

        Assert.True(second.Aborted);
        Assert.Contains(logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("same-tick repeat request"));
        Assert.Equal(stampedAfterFirst, state.LastHaloEnchantAttemptUtc);
        Assert.Equal(96, state.Halo);
        Assert.Equal(1_000, state.ContributionPoints);
    }

    [Fact]
    public async Task HaloEnchant_NonSameTickAttempt_PassesTheGuard_AndDebitsMoneyAndCp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var characters = new FakeCharacterRepository();
        var (_, _, state) = Setup(zone, CharacterId, contributionPoints: 1_000);
        state.Halo = 10;
        state.LastHaloEnchantAttemptUtc = DateTime.UtcNow - TimeSpan.FromSeconds(1);
        var service = CreateService(characters);

        var outcome = await service.HaloEnchantAsync(zone, state, CharacterId, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(outcome.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(900, after!.ContributionPoints);
        Assert.Equal((CharacterId, -1_000_000L, 0), characters.LastAdjustMoney);
    }

    [Fact]
    public async Task Rebirth_AlreadyAtPathSpecificCap_RepliesFailure_WithoutDisconnecting_AndDoesNotMutate()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, CharacterId, rebirthCount: 6);
        var service = CreateService();

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(6, state.RebirthCount);
        Assert.Equal(MaxHighLevelExp, state.Exp2);
        Assert.Equal(10_000, state.ContributionPoints);
        Assert.Equal(0, state.Zone241Time);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(RebirthFrame, frame.Length);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
    }

    [Fact]
    public async Task Rebirth_AlreadyAtAbsoluteCap_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, CharacterId, rebirthCount: RebirthProgression.MaxRebirthGeneration);
        var service = CreateService();

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Equal(RebirthProgression.MaxRebirthGeneration, state.RebirthCount);
    }

    [Fact]
    public async Task Rebirth_Level1NotAtCap_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, CharacterId, 144);
        var service = CreateService();

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Rebirth_Level2NotAtCap_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, CharacterId, level2: 11);
        var service = CreateService();

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Rebirth_Exp2BelowThreshold_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, CharacterId, exp2: MaxHighLevelExp - 1);
        var service = CreateService();

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Rebirth_NotEnoughContributionPoints_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, CharacterId, contributionPoints: 9_999);
        var service = CreateService();

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task Rebirth_Success_ResetsExp2_IncrementsRebirthCount_DebitsCp_AndHeals()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, CharacterId);
        state.Life = 1;
        state.Mana = 1;
        var service = CreateService();
        var request = RebirthRequest();
        request.Data[3] = 77;

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, request, outcome);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(1, after!.RebirthCount);
        Assert.Equal(0, after.Exp2);
        Assert.Equal(0, after.ContributionPoints);
        Assert.Equal(10, after.Zone241Time);
        Assert.NotNull(after.Stats);
        var stats = after.Stats!.Value;
        Assert.True(stats.MaxLife > 0);
        Assert.Equal(stats.MaxLife, after.Life);
        Assert.Equal(stats.MaxMana, after.Mana);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(RebirthFrame + StateFlagFrame, frame.Length);

        var echo = frame.AsSpan(1);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(echo));
        Assert.Equal(11, BinaryPrimitives.ReadInt32LittleEndian(echo[4..]));
        Assert.Equal(77, echo[8 + 3]);

        var stateFlag = frame.AsSpan(RebirthFrame + 1);
        Assert.Equal(CharacterId, BinaryPrimitives.ReadInt32LittleEndian(stateFlag));
        Assert.Equal(14, BinaryPrimitives.ReadInt32LittleEndian(stateFlag[8..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(stateFlag[12..]));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(stateFlag[16..]));
        Assert.Equal(10, BinaryPrimitives.ReadInt32LittleEndian(stateFlag[20..]));
    }

    [Fact]
    public async Task Rebirth_Success_BroadcastsToAoiNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, CharacterId);
        var (_, neighborPipe, _) = Setup(zone, NeighborId);
        ZoneTestKit.DrainOutbound(pipe);
        var service = CreateService();

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var neighborFrame = ZoneTestKit.DrainOutbound(neighborPipe);
        Assert.Equal(StateFlagFrame, neighborFrame.Length);
        var payload = neighborFrame.AsSpan(1);
        Assert.Equal(14, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
    }

    [Fact]
    public async Task Rebirth_Zone241TimeAdjustmentFails_Aborts_AndDoesNotMutateAnything()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var characters = new FakeCharacterRepository { ThrowOnAdjustZone241Time = true };
        var (session, _, state) = Setup(zone, CharacterId);
        var service = CreateService(characters);

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Equal(0, state.RebirthCount);
        Assert.Equal(MaxHighLevelExp, state.Exp2);
        Assert.Equal(10_000, state.ContributionPoints);
    }

    [Fact]
    public async Task Rebirth_CannotBeRepeated_WithoutExp2BeingRegrownToFullBetweenAttempts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, CharacterId);
        var service = CreateService();

        var first = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.False(first.Aborted);
        Assert.True(zone.TryGetPlayer(CharacterId, out state));
        Assert.Equal(1, state!.RebirthCount);
        Assert.Equal(0, state.Exp2);

        var second = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), second);

        Assert.Equal(DisconnectReason.Faulted,
            session.DisconnectReason);
        Assert.Equal(1, state.RebirthCount);
    }
}
