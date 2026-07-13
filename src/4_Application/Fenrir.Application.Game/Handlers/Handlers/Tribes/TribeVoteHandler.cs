using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

public sealed class TribeVoteHandler(ITribeVoteService voteService, ILogger<TribeVoteHandler>? logger = null)
    : IAsyncPacketHandler<TribeVoteRequest>
{
    private const int SlotCount = 10;

    public async ValueTask HandleAsync(TribeVoteRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_TRIBE_VOTE_SEND received (character {CharacterId}, sort {Sort}, value {Value})",
            session.SessionId, zoneSession.CharacterId, packet.Sort, packet.Value);

        if (packet.Sort is not (1 or 3) || packet.Value is < 0 or >= SlotCount)
        {
            logger?.LogWarning(
                "Session {SessionId}: CZ_TRIBE_VOTE_SEND malformed (sort {Sort}, value {Value}) -- aborting",
                session.SessionId, packet.Sort, packet.Value);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var player) || player is null)
            return;

        var slot = (byte)packet.Value;

        var result = packet.Sort == 1
            ? await voteService.RegisterCandidacyAsync(player, slot, cancellationToken)
            : await voteService.CastVoteAsync(player, slot, cancellationToken);

        switch (result.Action)
        {
            case TribeVoteAction.Accept:
            case TribeVoteAction.RejectNoAbort:
                session.Send(new TribeVoteResponse
                    { Result = result.Result, Sort = packet.Sort, Value = packet.Value });
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }
}
