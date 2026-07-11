using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneCenterBroadcastIngestorSiegeMappingTests
{
    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize(maps);
        return registry;
    }

    private static ZoneCenterBroadcastIngestor CreateIngestor(ZoneCenterSiegeState state, ZoneRegistry registry)
    {
        return new ZoneCenterBroadcastIngestor(state, registry, NullLogger<ZoneCenterBroadcastIngestor>.Instance);
    }

    private static byte[] Payload(params int[] int32Fields)
    {
        var data = new byte[ZoneCenterBroadcastIngestor.PayloadSize];
        for (var i = 0; i < int32Fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 4), int32Fields[i]);

        return data;
    }

    [Theory]
    [InlineData(64, 1)]
    [InlineData(70, 23)]
    [InlineData(99, 22)]
    [InlineData(100, 23)]
    public void Ingest_Zone175_WritesTheMappedState_NotTheRawCode(int eventCode, int expectedState)
    {
        var state = new ZoneCenterSiegeState();
        var ingestor = CreateIngestor(state, CreateRegistry(1));

        ingestor.Ingest(eventCode, Payload(2, 5));

        Assert.Equal(expectedState, state.GetZone175(2, 5));
    }

    [Theory]
    [InlineData(403, 1)]
    [InlineData(408, 4)]
    [InlineData(410, 0)]
    public void Ingest_Zone267_WritesTheMappedState_NotTheRawCode(int eventCode, int expectedState)
    {
        var state = new ZoneCenterSiegeState();
        var ingestor = CreateIngestor(state, CreateRegistry(1));

        state.SetZone267(2, 9);
        ingestor.Ingest(eventCode, Payload(2));

        Assert.Equal(expectedState, state.GetZone267(2));
    }

    [Fact]
    public void Ingest_Zone267NoOpCode402_LeavesTheSlotUntouched()
    {
        var state = new ZoneCenterSiegeState();
        state.SetZone267(1, 4);
        var ingestor = CreateIngestor(state, CreateRegistry(1));

        ingestor.Ingest(402, Payload(1));

        Assert.Equal(4, state.GetZone267(1));
    }

    [Theory]
    [InlineData(411, DenOfRebirthChallengeState.ChallengeStarted)]
    [InlineData(412, DenOfRebirthChallengeState.Ended)]
    [InlineData(413, DenOfRebirthChallengeState.Ended)]
    [InlineData(414, DenOfRebirthChallengeState.Ended)]
    [InlineData(415, DenOfRebirthChallengeState.Idle)]
    public void Ingest_Zone241_WritesTheMappedChallengeState_ForTheInstanceIndex(int eventCode,
        DenOfRebirthChallengeState expected)
    {
        var state = new ZoneCenterSiegeState();
        var ingestor = CreateIngestor(state, CreateRegistry(1));

        ingestor.Ingest(eventCode, Payload(3));

        Assert.Equal(expected, state.GetZone241(3));
        Assert.Equal(DenOfRebirthChallengeState.Idle, state.GetZone241(0));
    }

    [Fact]
    public void Ingest_Zone241_OutOfRangeInstance_DoesNotWrite()
    {
        var state = new ZoneCenterSiegeState();
        var ingestor = CreateIngestor(state, CreateRegistry(1));

        ingestor.Ingest(411, Payload(ZoneCenterSiegeState.Zone241Instances + 5));

        for (var instance = 0; instance < ZoneCenterSiegeState.Zone241Instances; instance++)
            Assert.Equal(DenOfRebirthChallengeState.Idle, state.GetZone241(instance));
    }

    [Theory]
    [InlineData(1502, 1)]
    [InlineData(1504, 3)]
    [InlineData(1507, 0)]
    public void Ingest_Zone335_WritesTheMappedScalar_NotTheRawCode(int eventCode, int expectedState)
    {
        var state = new ZoneCenterSiegeState();
        var ingestor = CreateIngestor(state, CreateRegistry(1));

        state.SetZone335(9);
        ingestor.Ingest(eventCode, Payload());

        Assert.Equal(expectedState, state.Zone335);
    }

    [Fact]
    public void Ingest_Zone335NoOpCode1501_LeavesTheScalarUntouched()
    {
        var state = new ZoneCenterSiegeState();
        state.SetZone335(4);
        var ingestor = CreateIngestor(state, CreateRegistry(1));

        ingestor.Ingest(1501, Payload(3));

        Assert.Equal(4, state.Zone335);
    }
}
