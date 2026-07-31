using System.IO.Pipelines;
using System.Net;
using Fenrir.Core.Wire;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Sessions;

public sealed class ZoneClientSession(
    long sessionId,
    IDuplexPipe transport,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : ClientSession(sessionId, transport, FenrirServer.Zone, remoteEndPoint, logger), IZoneSession
{
    public ZoneSessionState State { get; private set; } = ZoneSessionState.Connected;

    public int? AccountId { get; private set; }

    public int? CharacterId { get; private set; }

    public Guid? AccountSessionToken { get; private set; }

    public short AccountGrade { get; private set; }

    public short? TargetMapId { get; private set; }

    public bool IsGm => MeetsGmTier(GmCommandTier.Basic);

    public IZoneActor? CurrentZone { get; set; }

    public bool IsZoneTransferPending { get; private set; }

    public bool MeetsGmTier(GmCommandTier tier)
    {
        return AccountGrade >= (short)tier;
    }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return global::Fenrir.Protocol.Game.ZoneSessionStateGate.Allows(State, opcode);
    }

    public void MarkTicketConsumed(int accountId, int characterId, Guid? sessionToken = null, short accountGrade = 0,
        short targetMapId = 0)
    {
        var previous = State;
        AccountId = accountId;
        CharacterId = characterId;
        AccountSessionToken = sessionToken;
        AccountGrade = accountGrade;
        TargetMapId = targetMapId == 0 ? null : targetMapId;
        State = ZoneSessionState.TicketConsumed;
        LogSessionStateChanged(previous, State);
    }

    public void MarkRegistering()
    {
        var previous = State;
        State = ZoneSessionState.Registering;
        LogSessionStateChanged(previous, State);
    }

    public void MarkInWorld()
    {
        var previous = State;
        State = ZoneSessionState.InWorld;
        LogSessionStateChanged(previous, State);
    }

    public void MarkZoneTransferPending()
    {
        IsZoneTransferPending = true;
    }

    public void ClearZoneTransferPending()
    {
        IsZoneTransferPending = false;
    }
}
