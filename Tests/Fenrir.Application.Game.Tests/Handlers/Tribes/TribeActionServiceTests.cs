using System.Buffers.Binary;
using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

// CZ_TRIBE_WORK_SEND tSort 11 (Max Rebirth) -- cluster C14: HandleRebirthAsync's own gates against the newly
// real Level2/Exp2/RebirthCount fields, and its success mutation (Exp2 reset, RebirthCount+1, CP debit, full
// heal, sort-14 AOI broadcast).
public class TribeActionServiceTests
{
    private const int CharacterId = 10;
    private const int NeighborId = 20;

    // The threshold ReturnHighExpValue(12) resolves to -- see HighLevelExpTable's own remarks.
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

    private static TribeActionService CreateService()
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize([1]);

        // A level-145 catalog row is needed for EquipmentService.RecomputeStats' own MaxLife/MaxMana lookup
        // (the success test's "full heal" assertion reads it back from the freshly recomputed Stats).
        var levels = new Dictionary<short, LevelRowDto> { [145] = WorldDataTestRows.Level(145) }
            .ToFrozenDictionary();

        return new TribeActionService(registry, new FakeTribeRepository(), new FakeCharacterRepository(),
            ZoneTestKit.EmptyWorldData(levelsByLevel: levels), NullLogger<TribeActionService>.Instance);
    }

    private static TribeActionRequest RebirthRequest()
    {
        return new TribeActionRequest { Sort = 11, Data = new byte[100] };
    }

    /// <summary>Mirrors <c>TribeActionHandler.Respond</c> -- the actor's own reply is handler-owned plumbing.</summary>
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
    public async Task Rebirth_AlreadyAtRealCap_Aborts_AndDoesNotMutate()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, CharacterId, rebirthCount: 6);
        var service = CreateService();

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        Assert.Equal(6, state.RebirthCount);
        Assert.Equal(MaxHighLevelExp, state.Exp2);
    }

    [Fact]
    public async Task Rebirth_Level1NotAtCap_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, CharacterId, level: 144);
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
        request.Data[3] = 77; // arbitrary payload byte -- must round-trip verbatim on the echo

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, request, outcome);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(CharacterId, out var after));
        Assert.Equal(1, after!.RebirthCount);
        Assert.Equal(0, after.Exp2);
        Assert.Equal(0, after.ContributionPoints);
        // Full heal to the FRESHLY recomputed max (not whatever MaxLife/MaxMana happened to hold before) --
        // same "SetIntegerUp to the new max, not a clamp" posture as tSort 6/10 elsewhere in this service.
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
        Assert.Equal(77, echo[8 + 3]); // Data echoed back verbatim

        // RebirthFrame already includes its own 1-byte header; skip it whole, then the second frame's own header.
        var stateFlag = frame.AsSpan(RebirthFrame + 1);
        Assert.Equal(CharacterId, BinaryPrimitives.ReadInt32LittleEndian(stateFlag));
        Assert.Equal(14, BinaryPrimitives.ReadInt32LittleEndian(stateFlag[8..])); // Sort
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(stateFlag[12..])); // Value01 = ContributionPoints
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(stateFlag[16..])); // Value02 = RebirthCount
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(stateFlag[20..])); // Value03 (Zone241Time, not modeled)
    }

    [Fact]
    public async Task Rebirth_Success_BroadcastsToAoiNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, CharacterId);
        var (_, neighborPipe, _) = Setup(zone, NeighborId);
        ZoneTestKit.DrainOutbound(pipe); // neighbor's own Enter-broadcast join packet, not under test
        var service = CreateService();

        var outcome = await service.RebirthAsync(zone, state, CharacterId, CancellationToken.None);
        Respond(session, RebirthRequest(), outcome);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var neighborFrame = ZoneTestKit.DrainOutbound(neighborPipe);
        Assert.Equal(StateFlagFrame, neighborFrame.Length);
        var payload = neighborFrame.AsSpan(1);
        Assert.Equal(14, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
    }
}
