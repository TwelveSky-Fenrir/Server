using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TRADE_ASK_SEND (opcode 47). Same server exceptions as duel: inter-tribe trade is refused
///     (Quit()) except on maps 37/119/124. Target resolved within the asker's own zone only. Level uses
///     <see cref="PlayerRuntimeState.Level" /> alone (aLevel2 not modeled -- same gap as party's
///     cumulative-level check).
/// </summary>
public sealed class TradeInviteHandler(TradeRegistry trades) : IInlinePacketHandler<TradeInviteRequest>
{
    public void Handle(in TradeInviteRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var askerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(askerId, out var asker) || asker is null)
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
            session.Send(new TradeAnswerResponse { Answer = 4 });
            return;
        }

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && asker.Tribe != target.Tribe)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        switch (trades.TryAsk(askerId, target.CharacterId))
        {
            case TradeAskOutcome.AskerBusy:
                session.Send(new TradeAnswerResponse { Answer = 3 });
                return;
            case TradeAskOutcome.TargetBusy:
                session.Send(new TradeAnswerResponse { Answer = 5 });
                return;
            case TradeAskOutcome.Sent:
                target.Session.Send(new TradeInviteResponse { AvatarName = asker.Name, Level = asker.Level });
                return;
        }
    }
}
