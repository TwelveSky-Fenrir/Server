using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneEventBroadcasterTests
{
    private static int OneFrame => FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    [Fact]
    public void AnnounceZone038Winner_UpdatesWorldStateAndBroadcastsSortAndTribeId_ToEveryZone()
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

        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);

        broadcaster.AnnounceZone038Winner(2);

        Assert.Equal((byte?)2, worldState.World.Zone038WinTribe);

        foreach (var pipe in new[] { pipeA, pipeB })
        {
            var frame = ZoneTestKit.DrainOutbound(pipe);
            Assert.Equal(OneFrame, frame.Length);
            var payload = frame.AsSpan(1);
            Assert.Equal(38, BinaryPrimitives.ReadInt32LittleEndian(payload));
            Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        }
    }

    [Fact]
    public void AnnounceTribeSymbolBattleStarted_OpensTheWindow_AndBroadcastsSort40()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);

        broadcaster.AnnounceTribeSymbolBattleStarted();

        Assert.True(worldState.World.TribeSymbolBattle);
        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
    }

    [Fact]
    public void AnnounceTribeSymbolBattleEnded_ClosesTheWindow_AndBroadcastsSort45()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        broadcaster.AnnounceTribeSymbolBattleStarted();
        ZoneTestKit.DrainOutbound(pipe);

        broadcaster.AnnounceTribeSymbolBattleEnded();

        Assert.False(worldState.World.TribeSymbolBattle);
        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(45, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
    }

    [Theory]
    [InlineData((byte)2, (byte)1)] // tribe slot 2 contested, tribe 1 wins it
    [InlineData((byte)4, (byte)3)] // the neutral monster-guarded slot, tribe 3 wins it
    public void AnnounceSymbolResolved_UpdatesTheRightSlot_AndBroadcastsSort42WithIndexAndWinner(byte symbolIndex,
        byte winnerTribeId)
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);

        broadcaster.AnnounceSymbolResolved(symbolIndex, winnerTribeId);

        if (symbolIndex == WorldStateService.TribeCount)
            Assert.Equal(winnerTribeId, worldState.World.MonsterSymbol);
        else
            Assert.Equal(winnerTribeId == symbolIndex, worldState.GetTribe(symbolIndex).HasSymbol);

        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(42, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(symbolIndex, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(winnerTribeId, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
    }

    [Fact]
    public void AnnounceAllianceOffer_UpsertsTheOffer_AndBroadcastsSort46WithBothTribesAndAcceptedFlag()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);

        broadcaster.AnnounceAllianceOffer(0, 1, true);

        Assert.True(worldState.TryGetAllianceOffer(0, 1, out var offer));
        Assert.True(offer.IsAccepted);

        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(46, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(payload[12..]));
    }

    [Fact]
    public void PlayerWithNoZoneEntry_NeverReceivesAnything()
    {
        var registry = CreateRegistry(1);
        var worldState = CreateWorldState();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);

        // No player ever entered -- must not throw when every zone's player list is empty.
        broadcaster.AnnounceZone038Winner(0);

        Assert.Equal((byte?)0, worldState.World.Zone038WinTribe);
    }

    [Fact]
    public void AnnounceTribeSymbolBattleStarted_LogsAScope_TaggedAsStarted()
    {
        var registry = CreateRegistry(1);
        var worldState = CreateWorldState();
        var logger = new CapturingLogger<ZoneEventBroadcaster>();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, logger);

        broadcaster.AnnounceTribeSymbolBattleStarted();

        var scope = Assert.Single(logger.Scopes);
        var props = CapturingLogger<ZoneEventBroadcaster>.PropertiesOf(scope);
        Assert.Equal("Started", props.Single(p => p.Key == "SymbolBattlePhase").Value);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public void AnnounceTribeSymbolBattleEnded_LogsAScope_TaggedAsEnded()
    {
        var registry = CreateRegistry(1);
        var worldState = CreateWorldState();
        var logger = new CapturingLogger<ZoneEventBroadcaster>();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, logger);
        broadcaster.AnnounceTribeSymbolBattleStarted();

        broadcaster.AnnounceTribeSymbolBattleEnded();

        var props = CapturingLogger<ZoneEventBroadcaster>.PropertiesOf(logger.Scopes[^1]);
        Assert.Equal("Ended", props.Single(p => p.Key == "SymbolBattlePhase").Value);
    }

    [Theory]
    [InlineData((byte)2, (byte)1)]
    [InlineData((byte)4, (byte)3)]
    public void AnnounceSymbolResolved_LogsAScope_WithTheSlotAndWinner(byte symbolIndex, byte winnerTribeId)
    {
        var registry = CreateRegistry(1);
        var worldState = CreateWorldState();
        var logger = new CapturingLogger<ZoneEventBroadcaster>();
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, logger);

        broadcaster.AnnounceSymbolResolved(symbolIndex, winnerTribeId);

        var scope = Assert.Single(logger.Scopes);
        var props = CapturingLogger<ZoneEventBroadcaster>.PropertiesOf(scope);
        Assert.Equal(symbolIndex, props.Single(p => p.Key == "SymbolIndex").Value);
        Assert.Equal(winnerTribeId, props.Single(p => p.Key == "WinnerTribeId").Value);
    }
}
