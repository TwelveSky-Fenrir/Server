using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ValleyWarCenterRelayCodesTests
{
    private static int OneFrame => FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();

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

    [Theory]
    [InlineData(600, false)]
    [InlineData(601, true)]
    [InlineData(658, true)]
    [InlineData(675, true)]
    [InlineData(676, false)]
    public void IsInRelayRange_MatchesTheDeclaredBoundaries(int eventCode, bool expected)
    {
        Assert.Equal(expected, ValleyWarCenterRelayCodes.IsInRelayRange(eventCode));
    }

    [Theory]
    [InlineData(600, false)]
    [InlineData(601, true)]
    [InlineData(610, true)]
    [InlineData(611, false)]
    public void IsGodIndex2Cluster_MatchesTheDeclaredBoundaries(int eventCode, bool expected)
    {
        Assert.Equal(expected, ValleyWarCenterRelayCodes.IsGodIndex2Cluster(eventCode));
    }

    [Theory]
    [InlineData(610, false)]
    [InlineData(611, true)]
    [InlineData(615, true)]
    [InlineData(616, false)]
    public void IsMonsterSiegeCluster_MatchesTheDeclaredBoundaries(int eventCode, bool expected)
    {
        Assert.Equal(expected, ValleyWarCenterRelayCodes.IsMonsterSiegeCluster(eventCode));
    }

    [Theory]
    [InlineData(601)]
    [InlineData(605)]
    [InlineData(610)]
    [InlineData(611)]
    [InlineData(613)]
    [InlineData(615)]
    [InlineData(616)]
    [InlineData(640)]
    [InlineData(658)]
    [InlineData(661)]
    [InlineData(670)]
    [InlineData(675)]
    public void Ingest_EveryCodeAcrossTheValleyOfDeceasedCenterRelayRange_WritesNoState_ButStillRelays(
        int eventCode)
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1]);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry,
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        var beforeZone049 = ZoneCenterSiegeState.Zone049Slots;
        ingestor.Ingest(eventCode, Payload(0, 0));

        for (var slot = 0; slot < beforeZone049; slot++)
            Assert.Equal(0, state.GetZone049State(slot));
        Assert.Equal(0, state.Zone335);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(eventCode, ReadFrame(frame).Sort);
    }
}
