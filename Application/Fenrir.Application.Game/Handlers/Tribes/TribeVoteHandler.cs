using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_VOTE_SEND (opcode 83). Sort 1 (candidacy) and sort 3 (vote) each additionally require
///     <c>mWorldInfo-&gt;mTribeVoteState[tribe]</c> to be in the matching phase (1 for candidacy, 2 for
///     voting) -- a center-server-driven per-tribe election window that has no home in Fenrir yet, the same
///     class of gap as <see cref="TribeActionHandler" />'s tribe-skill (tSort 5) and Max Rebirth (tSort 11)
///     gates: real, but currently unreachable by design, not by omission, since nothing ever opens the
///     window. Sort 2 (client-side candidacy reset) is compiled out in this build (<c>MG5ORIGIN</c>).
/// </summary>
public sealed class TribeVoteHandler : IInlinePacketHandler<TribeVoteRequest>
{
    private const int SlotCount = 10;

    public void Handle(in TribeVoteRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (packet.Sort is not (1 or 3) || packet.Value is < 0 or >= SlotCount)
        {
        }

        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
