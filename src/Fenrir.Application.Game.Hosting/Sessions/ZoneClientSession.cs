using System.IO.Pipelines;
using System.Net;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Wire;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Transport;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Sessions;

public sealed class ZoneClientSession(
    long sessionId,
    IDuplexPipe transport,
    short listenerMapId,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null,
    OutboundBufferAdmissionGate? outboundAdmissionGate = null)
    : ClientSession(sessionId, transport, FenrirServer.Zone, remoteEndPoint, logger, outboundAdmissionGate),
        IZoneSession
{
    public short ListenerMapId { get; } = listenerMapId;

    public ZoneSessionState State { get; private set; } = ZoneSessionState.Connected;

    public int? AccountId { get; private set; }

    public int? CharacterId { get; private set; }

    public Guid? AccountSessionToken { get; private set; }

    public short AccountGrade { get; private set; }

    public short? TargetMapId { get; private set; }

    public bool IsGm => MeetsGmTier(GmCommandTier.Basic);

    public IZoneActor? CurrentZone { get; set; }

    public bool IsZoneTransferPending { get; private set; }

    public bool IsZoneTransferHandoffCommitted { get; private set; }

    public bool MeetsGmTier(GmCommandTier tier)
    {
        return AccountGrade >= (short)tier;
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
        var previous = State;
        IsZoneTransferPending = true;
        IsZoneTransferHandoffCommitted = false;
        State = ZoneSessionState.Leaving;
        LogSessionStateChanged(previous, State);
    }

    public void ConfirmZoneTransferHandoff()
    {
        if (IsZoneTransferPending)
            IsZoneTransferHandoffCommitted = true;
    }

    public void RevokeZoneTransferHandoffCommitment()
    {
        IsZoneTransferHandoffCommitted = false;
    }

    public void ClearZoneTransferPending()
    {
        var previous = State;
        IsZoneTransferPending = false;
        IsZoneTransferHandoffCommitted = false;
        State = ZoneSessionState.InWorld;
        LogSessionStateChanged(previous, State);
    }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return ZoneSessionStateGate.Allows(State, opcode);
    }

    public override bool ShouldWithholdOpcode(byte opcode)
    {
        return ZoneTransferFreezeGate.ShouldWithhold(IsZoneTransferPending, opcode,
            Opcodes.Zone.Incoming.ZoneTransferCancel, Opcodes.Zone.Incoming.ZoneHandshake,
            Opcodes.Zone.Incoming.EnterWorld);
    }
}
