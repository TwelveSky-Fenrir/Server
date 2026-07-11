using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests;

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
