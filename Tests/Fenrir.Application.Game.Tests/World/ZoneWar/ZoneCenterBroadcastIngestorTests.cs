using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneCenterBroadcastIngestorTests
{
    private static int OneFrame => FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize(maps);
        return registry;
    }

    private static byte[] Payload(params int[] int32Fields)
    {
        var data = new byte[ZoneCenterBroadcastIngestor.PayloadSize];
        for (var i = 0; i < int32Fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 4), int32Fields[i]);

        return data;
    }

    private static (int Sort, byte[] Data) ReadFrame(byte[] frame)
    {
        var payload = frame.AsSpan(1);
        var sort = BinaryPrimitives.ReadInt32LittleEndian(payload);
        return (sort, payload[4..].ToArray());
    }

    [Fact]
    public void Ingest_Zone175Code_WritesTheCell_AndRelaysSameSortAndPayload_ToEveryZone()
    {
        var registry = CreateRegistry(1, 2);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(1);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(2);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1)));
        registry[2].Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionB, 2)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        registry[2].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(70, Payload(2, 5));

        Assert.Equal(70, state.GetZone175(2, 5));

        foreach (var pipe in new[] { pipeA, pipeB })
        {
            var frame = ZoneTestKit.DrainOutbound(pipe);
            Assert.Equal(OneFrame, frame.Length);
            var (sort, data) = ReadFrame(frame);
            Assert.Equal(70, sort);
            Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(data));
            Assert.Equal(5, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4)));
        }
    }

    [Fact]
    public void Ingest_Zone175ResetCode_ReturnsTheCellToIdle()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        state.SetZone175(1, 3, 88);
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(ZoneCenterBroadcastIngestor.Zone175ResetEventCode, Payload(1, 3));

        Assert.Equal(0, state.GetZone175(1, 3));
    }

    [Fact]
    public void Ingest_Zone175Code_OutOfRangeCell_DoesNotWrite_ButStillRelays()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(70, Payload(99, 0));

        // No throw, no write for any valid cell -- and the relay still happens unconditionally.
        for (var instance = 0; instance < ZoneCenterSiegeState.Zone175Instances; instance++)
        for (var slot = 0; slot < ZoneCenterSiegeState.Zone175Slots; slot++)
            Assert.Equal(0, state.GetZone175(instance, slot));

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(70, ReadFrame(frame).Sort);
    }

    [Fact]
    public void Ingest_Zone267Code_WritesThePerTribeSlot()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(405, Payload(2));

        Assert.Equal(405, state.GetZone267(2));
        Assert.Equal(0, state.GetZone267(0));
    }

    [Fact]
    public void Ingest_DtmCode_WritesTheEffectValueForTheGivenTribe()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(ZoneCenterBroadcastIngestor.DtmEventCode, Payload(3, 7));

        Assert.Equal(7, state.GetZone038DtmValue(3));
        Assert.Equal(0, state.GetZone038DtmValue(0));
    }

    [Fact]
    public void Ingest_Zone335Code_SetsTheSharedScalar_ResetCodeReturnsToIdle()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(1503, Payload());
        Assert.Equal(1503, state.Zone335);

        ingestor.Ingest(ZoneCenterBroadcastIngestor.Zone335ResetEventCode, Payload());
        Assert.Equal(0, state.Zone335);
    }

    [Theory]
    [InlineData(416)] // Field of Loyalty / war-lifecycle
    [InlineData(500)] // alliance revocation
    [InlineData(628)] // Valkey of Deceased / monster siege / Valley of Tears (upper bound)
    [InlineData(665)] // Valley of Deceased (RW) -- dead case labels
    [InlineData(674)] // rank/RFC/cape/skill notification
    [InlineData(760)] // Proving Grounds / EXP Tower -- commented-out assignments
    [InlineData(1512)] // dead popup-state code
    [InlineData(411)] // Zone241 Den of Rebirth -- recognized/reachable but unbound, see GAP 1
    [InlineData(999999)] // genuinely unrecognized
    public void Ingest_DeadOrUnboundOrUnrecognizedCode_WritesNoState_ButStillRelays(int eventCode)
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(eventCode, Payload(0, 0));

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(eventCode, ReadFrame(frame).Sort);
    }

    [Fact]
    public void Ingest_PingCode_BroadcastsAnEmptyPingFirst_ThenTheNormalRelay_ToEveryZone()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        var originalPayload = Payload(11, 22);
        ingestor.Ingest(ZoneCenterBroadcastIngestor.PingEventCode, originalPayload);

        var frames = ZoneTestKit.DrainOutbound(pipe);
        // Two full frames back-to-back on the same pipe.
        Assert.Equal(OneFrame * 2, frames.Length);

        var first = ReadFrame(frames[..OneFrame]);
        var second = ReadFrame(frames[OneFrame..]);

        Assert.Equal(ZoneCenterBroadcastIngestor.PingEventCode, first.Sort);
        Assert.All(first.Data, b => Assert.Equal(0, b));

        Assert.Equal(ZoneCenterBroadcastIngestor.PingEventCode, second.Sort);
        Assert.Equal(11, BinaryPrimitives.ReadInt32LittleEndian(second.Data));
        Assert.Equal(22, BinaryPrimitives.ReadInt32LittleEndian(second.Data.AsSpan(4)));
    }

    [Fact]
    public void Ingest_WrongPayloadLength_Throws()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        Assert.Throws<ArgumentException>(() => ingestor.Ingest(70, new byte[10]));
    }

    [Fact]
    public void Ingest_OneRecipientsTransportAlreadyCompleted_DoesNotThrow_AndStillRelaysToEveryOtherRecipient()
    {
        var registry = CreateRegistry(1, 2);
        var (faultySession, faultyPipe) = ZoneTestKit.CreateSession(1);
        var (healthySession, healthyPipe) = ZoneTestKit.CreateSession(2);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(faultySession, 1)));
        registry[2].Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(healthySession, 2)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        registry[2].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(faultyPipe);
        ZoneTestKit.DrainOutbound(healthyPipe);

        // Simulates an ordinary disconnect race: this recipient's own transport already completed its
        // outbound pipe (SessionLoop's teardown already ran) while the character is still present in
        // Zone.Players (the zone's own Leave command hasn't drained yet) -- ClientSession.SendRaw throws
        // InvalidOperationException the instant it tries to write to an already-completed PipeWriter.
        faultyPipe.Output.Complete();

        var state = new ZoneCenterSiegeState();
        var logger = new CapturingLogger<ZoneCenterBroadcastIngestor>();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry, logger);

        // Regression guard: a bare `foreach (zone) foreach (player) player.Session.Send(...)` relay loop
        // with no per-recipient try/catch lets the FIRST faulted recipient's exception abort delivery to
        // every zone/player still left to visit, regardless of which of the two zones above is enumerated
        // first -- Ingest must never throw for this reason (its own doc comment already promises it "never
        // throws for an unrecognized or malformed-content event code"; an ordinary disconnect race must be
        // no worse).
        var exception = Record.Exception(() => ingestor.Ingest(70, Payload(2, 5)));
        Assert.Null(exception);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("10"));

        var frame = ZoneTestKit.DrainOutbound(healthyPipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(70, ReadFrame(frame).Sort);
    }

    [Fact]
    public void Ingest_NoZonesEverEntered_NeverThrows()
    {
        var registry = CreateRegistry(1);
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(70, Payload(0, 0));
        ingestor.Ingest(ZoneCenterBroadcastIngestor.PingEventCode, Payload());

        // The write still happens locally (it doesn't depend on any zone existing); what this test actually
        // guards is that relaying to an empty zone-player set never throws.
        Assert.Equal(70, state.GetZone175(0, 0));
    }
}
