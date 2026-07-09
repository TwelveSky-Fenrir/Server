using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Zone.Wire;
using Fenrir.Network.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Tests.Sessions;

// Session-state-transition logging (ClientSession.LogSessionStateChanged, centralized behind every Mark*
// setter that actually changes State): every real transition must produce exactly one Information-level
// entry naming both the previous and new state with real enum values, not a generic "state changed" marker.
public class ClientSessionStateLoggingTests
{
    [Fact]
    public void Zone_MarkInWorld_LogsPreviousAndNewStateAtInformation()
    {
        var logger = new CapturingLogger(LogLevel.Information);
        var session = new ZoneClientSession(9, new FakeDuplexPipe(), logger: logger);
        session.MarkTicketConsumed(1, 10);
        session.MarkRegistering();
        logger.Entries.Clear(); // isolate the transition under test from the two setup transitions above

        session.MarkInWorld();

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("9", entry.Message); // session id
        Assert.Contains(ZoneSessionState.Registering.ToString(), entry.Message); // real previous state
        Assert.Contains(ZoneSessionState.InWorld.ToString(), entry.Message); // real new state
    }

    [Fact]
    public void Zone_MarkTicketConsumed_ThroughMarkInWorld_LogsOneEntryPerRealTransition()
    {
        var logger = new CapturingLogger(LogLevel.Information);
        var session = new ZoneClientSession(3, new FakeDuplexPipe(), logger: logger);

        session.MarkTicketConsumed(1, 10);
        session.MarkRegistering();
        session.MarkInWorld();

        Assert.Equal(3, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Information, e.Level));
        Assert.Contains(logger.Entries, e => e.Message.Contains(ZoneSessionState.TicketConsumed.ToString()));
        Assert.Contains(logger.Entries, e => e.Message.Contains(ZoneSessionState.Registering.ToString()));
        Assert.Contains(logger.Entries, e => e.Message.Contains(ZoneSessionState.InWorld.ToString()));
    }

    [Fact]
    public void Login_MarkAccountSessionToken_NeverChangesState_LogsNothing()
    {
        // MarkAccountSessionToken records an ancillary fact, not a State transition (see its own remarks) --
        // it must never call LogSessionStateChanged.
        var logger = new CapturingLogger(LogLevel.Information);
        var session = new LoginClientSession(1, new FakeDuplexPipe(), logger: logger);

        session.MarkAccountSessionToken(Guid.NewGuid());

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Login_MarkAuthenticated_LogsConnectedToAuthenticated()
    {
        var logger = new CapturingLogger(LogLevel.Information);
        var session = new LoginClientSession(5, new FakeDuplexPipe(), logger: logger);

        session.MarkAuthenticated(42);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("5", entry.Message); // session id
        Assert.Contains(LoginSessionState.Connected.ToString(), entry.Message);
        Assert.Contains(LoginSessionState.Authenticated.ToString(), entry.Message);
    }

    // A session with no logger wired up (the common case in most tests in this project) must behave exactly
    // as before this feature existed -- Mark* setters never touch a null logger.
    [Fact]
    public void Zone_MarkInWorld_NeverThrows_WhenNoLoggerWired()
    {
        var session = new ZoneClientSession(1, new FakeDuplexPipe());
        session.MarkTicketConsumed(1, 10);
        session.MarkRegistering();

        session.MarkInWorld();

        Assert.Equal(ZoneSessionState.InWorld, session.State);
    }
}
