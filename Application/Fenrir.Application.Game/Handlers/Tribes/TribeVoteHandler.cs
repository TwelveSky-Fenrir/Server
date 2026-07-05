using Fenrir.Application.Game.Handlers.Tribes.Services;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.ZoneWar;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_VOTE_SEND (opcode 83) -- Force Leader election. Whichever future GM-command/scheduled-job
///     surface opens/closes the candidacy/voting windows and tallies the winner drives
///     <see cref="Fenrir.Application.Game.World.ZoneWar.TribeVoteElection" /> directly -- this handler only
///     consumes the phase it finds. Sort 2 (client-side candidacy reset) is compiled out in this build
///     (MG5ORIGIN).
/// </summary>
public sealed class TribeVoteHandler(ITribeVoteService voteService) : IAsyncPacketHandler<TribeVoteRequest>
{
    private const int SlotCount = 10;

    public async ValueTask HandleAsync(TribeVoteRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (packet.Sort is not (1 or 3) || packet.Value is < 0 or >= SlotCount)
        {
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
                session.Send(new TribeVoteResponse { Result = result.Result, Sort = packet.Sort, Value = packet.Value });
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }
}
