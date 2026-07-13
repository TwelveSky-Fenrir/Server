using System.IO.Pipelines;
using System.Net;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Core.Wire;
using Fenrir.Application.Game.ZoneRuntime;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game;

public enum GmCommandTier : short
{
    Basic = 1,
    Elevated = 10,
    Admin = 100
}

public sealed class ZoneClientSession(
    long sessionId,
    IDuplexPipe transport,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : ClientSession(sessionId, transport, FenrirServer.Zone, remoteEndPoint, logger)
{
    public ZoneSessionState State { get; private set; } = ZoneSessionState.Connected;

    public int? AccountId { get; private set; }

    public int? CharacterId { get; private set; }

    public Guid? AccountSessionToken { get; private set; }

    public short AccountGrade { get; private set; }

    public bool IsGm => MeetsGmTier(GmCommandTier.Basic);

    public IZoneActor? CurrentZone { get; set; }

    public bool IsCrossShardTransferPending { get; private set; }

    public bool MeetsGmTier(GmCommandTier tier)
    {
        return AccountGrade >= (short)tier;
    }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return ZoneSessionStateGate.Allows(State, opcode);
    }

    public void MarkTicketConsumed(int accountId, int characterId, Guid? sessionToken = null, short accountGrade = 0)
    {
        var previous = State;
        AccountId = accountId;
        CharacterId = characterId;
        AccountSessionToken = sessionToken;
        AccountGrade = accountGrade;
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

    public void MarkCrossShardTransferPending()
    {
        IsCrossShardTransferPending = true;
    }

    public void ClearCrossShardTransferPending()
    {
        IsCrossShardTransferPending = false;
    }
}
