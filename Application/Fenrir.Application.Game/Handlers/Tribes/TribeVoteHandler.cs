using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.ZoneWar;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_VOTE_SEND (opcode 83) -- Force Leader election. Sort 1 (candidacy, TRIBE_VOTE_V2 branch,
///     S04_MyWork02.cpp:11610+) requires <see cref="TribeVoteElection.Phase" /> to be
///     <see cref="TribeVotePhase.Candidacy" />; sort 3 (vote, same file's case 3) requires
///     <see cref="TribeVotePhase.Voting" />. Whichever future GM-command/scheduled-job surface opens/closes
///     those windows and tallies the winner drives <see cref="TribeVoteElection" /> directly -- this handler
///     only consumes the phase it finds. Sort 2 (client-side candidacy reset) is compiled out in this build
///     (MG5ORIGIN).
/// </summary>
public sealed class TribeVoteHandler(TribeVoteElection election) : IAsyncPacketHandler<TribeVoteRequest>
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

        if (packet.Sort == 1)
        {
            var outcome = await election.TryRegisterCandidacyAsync(player, slot, cancellationToken);
            if (outcome != TribeVoteCandidacyOutcome.Registered)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new TribeVoteResponse { Result = 0, Sort = packet.Sort, Value = packet.Value });
            return;
        }

        var voteOutcome = await election.TryCastVoteAsync(player, slot, cancellationToken);
        switch (voteOutcome)
        {
            case TribeVoteCastOutcome.Cast:
                session.Send(new TribeVoteResponse { Result = 0, Sort = packet.Sort, Value = packet.Value });
                return;
            case TribeVoteCastOutcome.AlreadyVotedThisWindow:
                // The one rejection that replies instead of disconnecting -- S04_MyWork02.cpp case 3's
                // aTribeVoteDate gate sends Result=1, not Quit().
                session.Send(new TribeVoteResponse { Result = 1, Sort = packet.Sort, Value = packet.Value });
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }
}
