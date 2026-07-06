using System.IO.Pipelines;
using System.Net;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Dispatch.Sessions;

/// <summary>
///     Zone-flow session: <c>Connected → TicketConsumed → Registering → InWorld</c>.
/// </summary>
public sealed class ZoneClientSession(long sessionId, IDuplexPipe transport, IPEndPoint? remoteEndPoint = null)
    : ClientSession(sessionId, transport, FenrirServer.Zone, remoteEndPoint)
{
    public ZoneSessionState State { get; private set; } = ZoneSessionState.Connected;

    /// <summary>Set by <see cref="MarkTicketConsumed" /> — the account the single-use session ticket resolved to.</summary>
    public int? AccountId { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkTicketConsumed" /> — the character the ticket committed to at login.
    /// </summary>
    public int? CharacterId { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkTicketConsumed" /> — the token carried in the consumed
    ///     <c>runtime.SessionTickets</c> row, threaded from the Login-side claim through
    ///     <c>usp_AccountSession_TransitionToGame</c>. Null until the ticket is consumed.
    /// </summary>
    public Guid? AccountSessionToken { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkTicketConsumed" /> — the account-grade fact carried in the consumed ticket,
    ///     originally <see cref="LoginClientSession.AccountGrade" /> at Login-side authentication (legacy
    ///     <c>uUserSort</c>). Zero (the default) means not elevated; never re-queried per action.
    /// </summary>
    public short AccountGrade { get; private set; }

    /// <summary>
    ///     Legacy's <c>uUserSort &lt; 1</c> elevation gate (Server/ts25zone/S04_MyWork04.cpp:1489 and identical
    ///     siblings at case 518/520/521) -- a strict binary gate, not a graduated permission: any positive grade
    ///     is treated as fully elevated.
    /// </summary>
    public bool IsGm => AccountGrade >= 1;

    // Re-pointed by the source zone's tick on each in-process map transfer. Unsynchronized: a reference
    // write is atomic and a stale read is benign — a command posted to the old zone just finds nothing there and is dropped.
    public IZoneActor? CurrentZone { get; set; }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return SessionStateGate.Allows(State, opcode);
    }

    // sessionToken/accountGrade are optional so every existing call site that never dealt with cross-process
    // duplicate-login tracking or GM elevation keeps compiling unchanged; ZoneHandshakeHandler (the one
    // production caller that consumes a real runtime.SessionTickets row) always supplies both.
    public void MarkTicketConsumed(int accountId, int characterId, Guid? sessionToken = null, short accountGrade = 0)
    {
        AccountId = accountId;
        CharacterId = characterId;
        AccountSessionToken = sessionToken;
        AccountGrade = accountGrade;
        State = ZoneSessionState.TicketConsumed;
    }

    public void MarkRegistering()
    {
        State = ZoneSessionState.Registering;
    }

    public void MarkInWorld()
    {
        State = ZoneSessionState.InWorld;
    }
}
