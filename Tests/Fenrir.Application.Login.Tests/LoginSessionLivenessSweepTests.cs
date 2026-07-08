using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests;

// Login-side counterpart to GameServer's SessionLivenessSweep: any Login connection -- pre-auth or
// post-auth alike -- idle strictly more than 60 seconds is forcibly disconnected
// (Server/ts25login/S07_MyGame01.cpp:37-73).
public class LoginSessionLivenessSweepTests
{
    [Fact]
    public void Sweep_DisconnectsASessionStrictlyPastTheIdleThreshold()
    {
        var registry = new SessionRegistry();
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        registry.Register(session);
        var sweep = new LoginSessionLivenessSweep(registry, NullLogger<LoginSessionLivenessSweep>.Instance);

        sweep.Sweep(session.LastActivityUtc + LoginSessionLivenessSweep.IdleTimeout + TimeSpan.FromTicks(1));

        Assert.Equal(DisconnectReason.IdleTimeout, session.DisconnectReason);
    }

    [Fact]
    public void Sweep_LeavesAFreshSessionConnected()
    {
        var registry = new SessionRegistry();
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        registry.Register(session);
        var sweep = new LoginSessionLivenessSweep(registry, NullLogger<LoginSessionLivenessSweep>.Instance);

        sweep.Sweep(session.LastActivityUtc);

        Assert.Null(session.DisconnectReason);
    }

    // Strict `>`, not `>=` -- see SessionRegistry.SnapshotIdle's own remarks.
    [Fact]
    public void Sweep_LeavesASessionExactlyAtTheThresholdConnected()
    {
        var registry = new SessionRegistry();
        var session = new LoginClientSession(1, new FakeDuplexPipe());
        registry.Register(session);
        var sweep = new LoginSessionLivenessSweep(registry, NullLogger<LoginSessionLivenessSweep>.Instance);

        sweep.Sweep(session.LastActivityUtc + LoginSessionLivenessSweep.IdleTimeout);

        Assert.Null(session.DisconnectReason);
    }
}
