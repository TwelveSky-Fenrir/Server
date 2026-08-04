using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

public sealed class TribeVoteHandler(ITribeVoteService voteService, ILogger<TribeVoteHandler>? logger = null)
    : IAsyncPacketHandler<TribeVoteRequest>
{
    private const int SlotCount = 10;

    public async ValueTask HandleAsync(TribeVoteRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_TRIBE_VOTE_SEND received (character {CharacterId}, sort {Sort}, value {Value})",
            session.SessionId, zoneSession.CharacterId, packet.Sort, packet.Value);

        if (packet.Sort is not (1 or 3))
        {
            logger?.LogWarning(
                "Session {SessionId}: CZ_TRIBE_VOTE_SEND carried invalid sort {Sort}; aborting",
                session.SessionId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort == 3 && packet.Value is < 0 or >= SlotCount)
        {
            logger?.LogWarning(
                "Session {SessionId}: CZ_TRIBE_VOTE_SEND vote carried invalid value {Value}; aborting",
                session.SessionId, packet.Value);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort == 1 && packet.Value is < 0 or >= SlotCount)
        {
            logger?.LogWarning(
                "Session {SessionId}: CZ_TRIBE_VOTE_SEND candidacy carried invalid value {Value}; ignoring",
                session.SessionId, packet.Value);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var player) || player is null)
            return;

        var slot = (byte)packet.Value;

        if (packet.Sort == 1)
        {
            var candidacyResult = await voteService.RegisterCandidacyAsync(player, slot, cancellationToken);
            if (candidacyResult.Action is TribeVoteAction.Accept or TribeVoteAction.RejectNoAbort)
                session.Send(new TribeVoteResponse
                    { Result = candidacyResult.Result, Sort = packet.Sort, Value = packet.Value });
            return;
        }

        var result = await voteService.CastVoteAsync(player, slot, cancellationToken);

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
