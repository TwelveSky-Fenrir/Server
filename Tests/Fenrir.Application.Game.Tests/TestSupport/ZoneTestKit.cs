using System.Buffers;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     Deterministic zone construction for tests: real <see cref="Zone" /> instances (no fakes of the unit
///     under test), real <see cref="ZoneClientSession" />s over in-memory pipes, and time driven exclusively
///     through <see cref="Zone.Tick" /> — no <c>RunAsync</c>, no timers, no sleeps.
/// </summary>
internal static class ZoneTestKit
{
    public static GameServerOptions Options(params short[] maps)
    {
        return new GameServerOptions { Maps = maps.Length == 0 ? [1] : maps };
    }

    public static Zone CreateZone(short mapId, GameServerOptions? options = null,
        DirtyTracker<int>? dirtyTracker = null, IReadOnlyList<ISimulationSystem>? simulationSystems = null)
    {
        var opts = options ?? Options(mapId);
        return new Zone(mapId, opts, new MovementRules(Microsoft.Extensions.Options.Options.Create(opts)),
            dirtyTracker ?? new DirtyTracker<int>(), simulationSystems ?? [], NullLogger<Zone>.Instance);
    }

    public static (ZoneClientSession Session, FakeDuplexPipe Pipe) CreateSession(long sessionId)
    {
        var pipe = new FakeDuplexPipe();
        return (new ZoneClientSession(sessionId, pipe), pipe);
    }

    public static PlayerEnterData EnterData(ZoneClientSession session, short mapId, string name = "Hero",
        float posX = 100f, float posY = 0f, float posZ = 100f, long flushSequence = 7)
    {
        return new PlayerEnterData(
            session,
            name,
            1,
            0,
            2,
            3,
            42,
            mapId,
            posX,
            posY,
            posZ,
            1.5f,
            800,
            840,
            300,
            320,
            flushSequence);
    }

    /// <summary>Drains every byte the session has written so far (empty array when nothing is pending).</summary>
    public static byte[] DrainOutbound(FakeDuplexPipe pipe)
    {
        if (!pipe.SessionToPeer.TryRead(out var result))
            return [];

        var bytes = result.Buffer.ToArray();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);
        return bytes;
    }
}
