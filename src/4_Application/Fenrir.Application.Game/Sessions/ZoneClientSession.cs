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

    /// <summary>Map de zone cible portée par le ticket de handover consommé à l'op11 ; <c>null</c> hors transfert.
    /// Prime sur le <c>MapId</c> relu de SQL à l'entrée-monde (le ticket est l'autorité de la map cible).</summary>
    public short? TargetMapId { get; private set; }

    public bool IsGm => MeetsGmTier(GmCommandTier.Basic);

    public IZoneActor? CurrentZone { get; set; }

    /// <summary>Vrai entre l'émission d'un <c>ZoneMoveResponse</c> (le client va fermer/rouvrir son socket) et la
    /// reconnexion : signale au teardown de connexion de <b>sauter</b> le self-kick de session de compte. S'applique
    /// aux transferts intra ET cross-zone (chemin unifié V2.2), d'où le nom neutre.</summary>
    public bool IsZoneTransferPending { get; private set; }

    public bool MeetsGmTier(GmCommandTier tier)
    {
        return AccountGrade >= (short)tier;
    }

    public override bool IsOpcodeAllowed(byte opcode)
    {
        return ZoneSessionStateGate.Allows(State, opcode);
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
