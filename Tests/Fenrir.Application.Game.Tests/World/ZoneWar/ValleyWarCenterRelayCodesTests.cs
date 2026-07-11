using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Pins down two facts for the Valley of the Deceased (Zone 200/297/298/299) Center-relay range
///     (601-675, see <see cref="ValleyWarCenterRelayCodes" />): (1) the catalog's own range/cluster
///     membership helpers, and (2) that <see cref="ZoneCenterBroadcastIngestor" />'s existing
///     <c>ApplyStateEffect</c> switch -- which already has no case label anywhere in this range -- produces
///     zero state effect plus an unconditional relay for every representative code across the whole 601-675
///     span, not just the two spot-checked values <c>ZoneCenterBroadcastIngestorTests</c> already covers
///     (628, 665). This is a pure regression/documentation guard: nothing here changes
///     <see cref="ZoneCenterBroadcastIngestor" /> itself, and this class's own remarks explain why no case
///     label should ever be added for this range.
/// </summary>
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
    [InlineData(601)] // God index=2 cluster, lower bound
    [InlineData(605)] // God index=2 cluster, interior
    [InlineData(610)] // God index=2 cluster, upper bound
    [InlineData(611)] // Monster Siege cluster, lower bound
    [InlineData(613)] // Monster Siege cluster, interior
    [InlineData(615)] // Monster Siege cluster, upper bound
    [InlineData(616)] // between the two dead clusters and the live gate/door/kill/win subset
    [InlineData(640)] // unassigned interior of the whole range
    [InlineData(658)] // immediately below the confirmed-live gate/door/kill/win subset (659-669)
    [InlineData(661)] // inside the live subset's own numeric span but itself never sent for this family
    [InlineData(670)] // immediately above the confirmed-live subset
    [InlineData(675)] // whole-range upper bound
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

        // No case label anywhere in this range means every mutable field this ingestor could have touched
        // stays at its own default -- spot-check the ones the switch could plausibly have mis-routed into
        // given adjacent ranges (Zone049 ends at 9, Zone267 starts at 402, Zone335 starts at 1501; none of
        // this range overlaps any of them, but a regression that widened a neighboring range's bounds would
        // show up here).
        for (var slot = 0; slot < beforeZone049; slot++)
            Assert.Equal(0, state.GetZone049State(slot));
        Assert.Equal(0, state.Zone335);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(eventCode, ReadFrame(frame).Sort);
    }
}
