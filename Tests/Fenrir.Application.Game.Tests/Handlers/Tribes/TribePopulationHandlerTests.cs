using System.Buffers.Binary;
using Fenrir.Application.Game.Handlers.Tribes;
using Fenrir.Application.Game.Handlers.Tribes.Services;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Tribes;

public class TribePopulationHandlerTests
{
    private static int OneFrame => FrameWriter.FrameSizeOf<TribePopulationResponse>();

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    [Fact]
    public void Handle_IgnoresRequestedZoneNumber_AndCountsEveryTribeAcrossEveryZone()
    {
        var registry = CreateRegistry(1, 2);

        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);
        var (sessionC, _) = ZoneTestKit.CreateSession(3);

        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1, tribe: 0)));
        registry[1].Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(sessionB, 1, tribe: 0)));
        registry[2].Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(sessionC, 2, tribe: 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        registry[2].Tick(TimeSpan.FromMilliseconds(50));

        var (querySession, pipe) = ZoneTestKit.CreateSession(4);
        var handler = new TribePopulationHandler(new TribePopulationService(registry));

        handler.Handle(new TribePopulationRequest { ZoneNumber = 999 }, querySession);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame * 4, bytes.Length);

        var expected = new (int ZoneNumber, int ConnectedUser)[] { (348, 2), (349, 1), (350, 0), (351, 0) };
        for (var i = 0; i < 4; i++)
        {
            var payload = bytes.AsSpan(i * OneFrame + 1);
            Assert.Equal(expected[i].ZoneNumber, BinaryPrimitives.ReadInt32LittleEndian(payload));
            Assert.Equal(expected[i].ConnectedUser, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        }
    }

    [Fact]
    public void Handle_NoPlayersAnywhere_SendsFourZeroCountResponses()
    {
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        var handler = new TribePopulationHandler(new TribePopulationService(registry));

        handler.Handle(new TribePopulationRequest { ZoneNumber = 1 }, session);

        var bytes = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(OneFrame * 4, bytes.Length);
        for (var i = 0; i < 4; i++)
            Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * OneFrame + 1 + 4)));
    }
}
