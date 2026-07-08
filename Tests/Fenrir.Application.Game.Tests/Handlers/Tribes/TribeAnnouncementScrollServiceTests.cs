using System.Buffers.Binary;
using System.Text;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Handlers.Tribes;
using Fenrir.Application.Game.Services.Tribes;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribeAnnouncementScrollServiceTests
{
    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    private static string ReadFixedString(ReadOnlySpan<byte> source)
    {
        var nul = source.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nul >= 0 ? source[..nul] : source);
    }

    [Fact]
    public void TryBroadcast_WithCharges_DecrementsCount_SendsSelfUpdate_AndRelaysToSameTribeAcrossZones()
    {
        var registry = CreateRegistry(1, 2);
        var (senderSession, senderPipe) = ZoneTestKit.CreateSession(1);
        var (sameTribeSession, sameTribePipe) = ZoneTestKit.CreateSession(2);
        var (otherTribeSession, otherTribePipe) = ZoneTestKit.CreateSession(3);
        senderSession.MarkTicketConsumed(1, 10);

        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(senderSession, 1, "Odin", tribe: 1)));
        registry[2].Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sameTribeSession, 2, tribe: 1)));
        registry[1].Post(ZoneCommand.Enter(30, ZoneTestKit.EnterData(otherTribeSession, 1, tribe: 2)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        registry[2].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(senderPipe);
        ZoneTestKit.DrainOutbound(sameTribePipe);
        ZoneTestKit.DrainOutbound(otherTribePipe);

        senderSession.CurrentZone = registry[1];
        registry.TryGetPlayer(10, out var sender);
        sender!.TribeNotifyScrollCount = 3;

        var service = new TribeAnnouncementScrollService(registry, new FakeGuildTribeBroadcastRelayQueue(),
            Options.Create(ZoneTestKit.Options()), NullLogger<TribeAnnouncementScrollService>.Instance);
        var succeeded = service.TryBroadcast(registry[1], sender, 10, senderSession, "Scroll used!");

        Assert.True(succeeded);

        var statFrameSize = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();
        var scrollFrameSize = FrameWriter.FrameSizeOf<TribeAnnouncementScrollResponse>();

        // sender: self stat update first, then its own broadcast copy (same-tribe fan-out includes self)
        var senderBytes = ZoneTestKit.DrainOutbound(senderPipe);
        Assert.Equal(statFrameSize + scrollFrameSize, senderBytes.Length);

        var statPayload = senderBytes.AsSpan(1, statFrameSize - 1);
        Assert.Equal(69, BinaryPrimitives.ReadInt32LittleEndian(statPayload));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(statPayload[4..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(statPayload[8..]));

        var scrollPayload = senderBytes.AsSpan(statFrameSize + 1);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(scrollPayload));
        Assert.Equal("Odin", ReadFixedString(scrollPayload.Slice(4, 13)));
        Assert.Equal("Scroll used!", ReadFixedString(scrollPayload[17..]));

        // same-tribe recipient in a different zone: just the broadcast copy
        var sameTribeBytes = ZoneTestKit.DrainOutbound(sameTribePipe);
        Assert.Equal(scrollFrameSize, sameTribeBytes.Length);

        // different-tribe recipient: nothing at all
        Assert.Empty(ZoneTestKit.DrainOutbound(otherTribePipe));

        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        registry.TryGetPlayer(10, out var afterTick);
        Assert.Equal(2, afterTick!.TribeNotifyScrollCount);
    }

    [Fact]
    public void TryBroadcast_WithNoCharges_ReturnsFalse_AndDoesNotBroadcast()
    {
        var registry = CreateRegistry(1);
        var (senderSession, senderPipe) = ZoneTestKit.CreateSession(1);
        senderSession.MarkTicketConsumed(1, 10);

        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(senderSession, 1, tribe: 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(senderPipe);

        senderSession.CurrentZone = registry[1];
        registry.TryGetPlayer(10, out var sender);
        sender!.TribeNotifyScrollCount = 0;

        var service = new TribeAnnouncementScrollService(registry, new FakeGuildTribeBroadcastRelayQueue(),
            Options.Create(ZoneTestKit.Options()), NullLogger<TribeAnnouncementScrollService>.Instance);
        var succeeded = service.TryBroadcast(registry[1], sender, 10, senderSession, "No charges");

        Assert.False(succeeded);
        Assert.Empty(ZoneTestKit.DrainOutbound(senderPipe));
    }

    [Fact]
    public void Handle_WithEmptyContent_Aborts()
    {
        // The empty-content gate lives on the handler itself, ahead of the service call -- exercise the real
        // handler here rather than the service, which never sees an empty Content at all.
        var registry = CreateRegistry(1);
        var (senderSession, _) = ZoneTestKit.CreateSession(1);

        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(senderSession, 1, tribe: 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        senderSession.CurrentZone = registry[1];
        registry.TryGetPlayer(10, out var sender);
        sender!.TribeNotifyScrollCount = 5;

        var handler = new TribeAnnouncementScrollHandler(
            new TribeAnnouncementScrollService(registry, new FakeGuildTribeBroadcastRelayQueue(),
                Options.Create(ZoneTestKit.Options()), NullLogger<TribeAnnouncementScrollService>.Instance));
        handler.Handle(new TribeAnnouncementScrollRequest { Content = "" }, senderSession);

        Assert.Equal(DisconnectReason.Faulted, senderSession.DisconnectReason);
    }
}
