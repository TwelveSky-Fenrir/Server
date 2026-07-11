using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     End-to-end proof that <see cref="SiegeZoneLiteralEventCatalog" />'s named constants drive
///     <see cref="ZoneCenterBroadcastIngestor.Ingest" /> identically to the raw numbers the existing
///     Zone267/Zone241/pure-relay tests already pin -- the catalog is purely a naming layer, never a second
///     source of truth for the numeric dispatch <see cref="SiegeEventStateMap" />/
///     <see cref="ZoneCenterBroadcastIngestor" /> already own.
/// </summary>
public class SiegeZoneLiteralEventCatalogIngestionTests
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

    private static int ReadSort(byte[] frame)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1));
    }

    [Theory]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267WritesState1EventCode, 1)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267WritesState4EventCode, 4)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267ResetEventCode, 0)]
    public void Ingest_ByNamedZone267Constant_WritesTheSameStateAsTheRawCode(int namedEventCode, int expectedState)
    {
        var state = new ZoneCenterSiegeState();
        state.SetZone267(2, 9); // seed so a reset-to-0 is observable
        var ingestor = new ZoneCenterBroadcastIngestor(state, CreateRegistry(1),
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(namedEventCode, Payload(2));

        Assert.Equal(expectedState, state.GetZone267(2));
    }

    [Fact]
    public void Ingest_Zone267NamedNoOpConstant_LeavesTheSlotUntouched()
    {
        var state = new ZoneCenterSiegeState();
        state.SetZone267(1, 4);
        var ingestor = new ZoneCenterBroadcastIngestor(state, CreateRegistry(1),
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(SiegeZoneLiteralEventCatalog.Zone267NoOpEventCode, Payload(1));

        Assert.Equal(4, state.GetZone267(1));
    }

    [Theory]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241ChallengeStartedEventCode, DenOfRebirthChallengeState.ChallengeStarted)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241FailureEventCodeA, DenOfRebirthChallengeState.Ended)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241FailureEventCodeB, DenOfRebirthChallengeState.Ended)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241SuccessEventCode, DenOfRebirthChallengeState.Ended)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241ReturnTownEventCode, DenOfRebirthChallengeState.Idle)]
    public void Ingest_ByNamedZone241Constant_WritesTheSameChallengeStateAsTheRawCode(int namedEventCode,
        DenOfRebirthChallengeState expected)
    {
        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, CreateRegistry(1),
            NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(namedEventCode, Payload(3));

        Assert.Equal(expected, state.GetZone241(3));
    }

    [Theory]
    [InlineData(SiegeZoneLiteralEventCatalog.WarHasStartedEventCode)]
    [InlineData(SiegeZoneLiteralEventCatalog.InstinctDefenseFormationInUseEventCode)]
    [InlineData(SiegeZoneLiteralEventCatalog.FolSuccessEventCode)]
    [InlineData(SiegeZoneLiteralEventCatalog.UnlabeledEventCode423)]
    [InlineData(SiegeZoneLiteralEventCatalog.FolSuccessDuplicateLabelEventCode)]
    [InlineData(SiegeZoneLiteralEventCatalog.FolAnnihilationFailedEventCode)]
    [InlineData(SiegeZoneLiteralEventCatalog.AllFactionsAllianceRevokedEventCode)]
    public void Ingest_ByNamedPureRelayConstant_WritesNoState_ButStillRelays(int namedEventCode)
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry, NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        ingestor.Ingest(namedEventCode, Payload(0, 0));

        // No Zone267/Zone241/Zone175/Zone335/DTM/tribe-bonus state anywhere changed from its default.
        for (byte tribeId = 0; tribeId < 4; tribeId++)
        {
            Assert.Equal(0, state.GetZone267(tribeId));
            Assert.Equal(0, state.GetZone038DtmValue(tribeId));
            Assert.Equal(0f, state.GetExperienceBonusRatio(tribeId));
        }

        for (var instance = 0; instance < ZoneCenterSiegeState.Zone241Instances; instance++)
            Assert.Equal(DenOfRebirthChallengeState.Idle, state.GetZone241(instance));

        Assert.Equal(0, state.Zone335);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(namedEventCode, ReadSort(frame));
    }

    [Fact]
    public void Ingest_TheKnownGapEventCode417_WritesNoState_ButStillRelays()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var state = new ZoneCenterSiegeState();
        var ingestor = new ZoneCenterBroadcastIngestor(state, registry, NullLogger<ZoneCenterBroadcastIngestor>.Instance);

        var gapCode = Assert.Single(SiegeZoneLiteralEventCatalog.KnownGapEventCodes);
        ingestor.Ingest(gapCode, Payload(0, 0));

        Assert.Equal(0, state.GetZone267(0));

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame, frame.Length);
        Assert.Equal(gapCode, ReadSort(frame));
    }
}
