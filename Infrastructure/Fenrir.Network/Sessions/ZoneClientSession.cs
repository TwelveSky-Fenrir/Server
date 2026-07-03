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

    /// <summary>
    ///     Set by <see cref="MarkTicketConsumed" /> — the account the single-use session ticket (ADR-0005) resolved to.
    ///     Null until then.
    /// </summary>
    public int? AccountId { get; private set; }

    /// <summary>
    ///     Set by <see cref="MarkTicketConsumed" /> — the character the ticket committed to at login time
    ///     (CL_DEMAND_ZONE_SERVER_INFO_SEND already fixed this choice; CZ_REGISTER_AVATAR_SEND's AvatarName is only
    ///     re-verified against it, wire contract §5.3).
    /// </summary>
    public int? CharacterId { get; private set; }

    /// <summary>
    ///     The zone actor currently simulating this session's character: null until world registration resolves
    ///     <c>character.MapId</c> against the shard's <c>ZoneRegistry</c>, then re-pointed by the SOURCE zone's
    ///     tick on each in-process map transfer (ADR-0012).
    /// </summary>
    /// <remarks>
    ///     Written by the registration handler or a zone tick, read by inline movement handlers on other
    ///     threads — safe without synchronization because a reference write is atomic and a STALE read is
    ///     benign by design: a command posted to the previous zone finds the character no longer tracked there
    ///     and is dropped, exactly like a command for a player who just disconnected.
    /// </remarks>
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
