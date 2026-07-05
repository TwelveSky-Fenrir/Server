using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TRADE_ASK_SEND (opcode 47) -- level uses <see cref="PlayerRuntimeState.Level" /> alone; aLevel2
///     isn't modeled, same gap as party's invite check.
/// </summary>
public sealed class TradeInviteHandler(ITradeInviteService tradeInviteService) : IInlinePacketHandler<TradeInviteRequest>
{
    public void Handle(in TradeInviteRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var askerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(askerId, out var asker) || asker is null)
            return;

        var result = tradeInviteService.Invite(zone, asker, packet.AvatarName);

        switch (result.Kind)
        {
            case TradeInviteResultKind.TargetNotFound:
                session.Send(new TradeAnswerResponse { Answer = 4 });
                return;
            case TradeInviteResultKind.MustDisconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case TradeInviteResultKind.AskerBusy:
                session.Send(new TradeAnswerResponse { Answer = 3 });
                return;
            case TradeInviteResultKind.TargetBusy:
                session.Send(new TradeAnswerResponse { Answer = 5 });
                return;
            case TradeInviteResultKind.Sent:
                zone.TryGetPlayer(result.TargetCharacterId, out var target);
                target!.Session.Send(new TradeInviteResponse { AvatarName = result.AskerName!, Level = result.AskerLevel });
                return;
        }
    }
}
