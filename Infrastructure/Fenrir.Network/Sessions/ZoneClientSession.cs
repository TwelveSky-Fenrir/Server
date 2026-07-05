using System.IO.Pipelines;
using System.Net;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.Sessions;

/// <summary>
///     Zone-flow session: <c>Connected → TicketConsumed → Registering → InWorld</c>
///     (Docs/protocol/M1_Legacy_Wire_Contract.md §1, §8.1 of the architecture reference).
/// </summary>
public sealed class ZoneClientSession(long sessionId, IDuplexPipe transport, IPEndPoint? remoteEndPoint = null)
    : ClientSession(sessionId, transport, FenrirServer.Zone, remoteEndPoint)
{
    public ZoneSessionState State { get; private set; } = ZoneSessionState.Connected;

    /// <summary>Set by <see cref="MarkTicketConsumed" /> — the account the single-use session ticket (ADR-0005) resolved to.</summary>
    public int? AccountId { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkTicketConsumed" /> — the character the ticket committed to at login (wire contract
    ///     §5.3).
    /// </summary>
    public int? CharacterId { get; private set; }

    // Re-pointed by the source zone's tick on each in-process map transfer (ADR-0012). Unsynchronized: a reference
    // write is atomic and a stale read is benign — a command posted to the old zone just finds nothing there and is dropped.
    public IZoneActor? CurrentZone { get; set; }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return SessionStateGate.Allows(State, opcode);
    }

    public void MarkTicketConsumed(int accountId, int characterId)
    {
        AccountId = accountId;
        CharacterId = characterId;
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
