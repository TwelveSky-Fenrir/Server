using Fenrir.Application.Game.Social.Duel;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_DUEL_ASK_SEND (opcode 43) -- map 124 (scripted-duel server) always refuses immediately.</summary>
public sealed class DuelAskHandler(DuelRegistry duels) : IInlinePacketHandler<DuelChallengeRequest>
{
    public void Handle(in DuelChallengeRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (zone.MapId == 124)
        {
            session.Send(new DuelAnswerResponse { Answer = 3 });
            return;
        }

        var challengerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(challengerId, out var challenger) || challenger is null)
            return;

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
        {
            session.Send(new DuelAnswerResponse { Answer = 4 });
            return;
        }

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && challenger.Tribe != target.Tribe)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        switch (duels.TryAsk(challengerId, target.CharacterId, packet.Sort == 1))
        {
            case DuelAskOutcome.ChallengerBusy:
                session.Send(new DuelAnswerResponse { Answer = 3 });
                return;
            case DuelAskOutcome.TargetBusy:
                session.Send(new DuelAnswerResponse { Answer = 5 });
                return;
            case DuelAskOutcome.Sent:
                target.Session.Send(new DuelChallengeResponse { AvatarName = challenger.Name, Sort = packet.Sort });
                return;
        }
    }
}
