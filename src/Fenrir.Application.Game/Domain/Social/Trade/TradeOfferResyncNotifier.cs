using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Social.Trade;

public static class TradeOfferResyncNotifier
{
    public static bool TryNotifyOpponent(TradeRegistry tradeRegistry, ZoneRegistry zoneRegistry,
        int mutatingCharacterId)
    {
        if (!tradeRegistry.TryGetSession(mutatingCharacterId, out var session) || session is null)
            return false;

        var opponentId = session.OpponentOf(mutatingCharacterId);

        if (!tradeRegistry.TryGetSession(opponentId, out var opponentSession) ||
            !ReferenceEquals(opponentSession, session))
            return false;

        if (!zoneRegistry.TryGetPlayer(opponentId, out var opponent))
            return false;

        var mutatingSide = session.SideOf(mutatingCharacterId);
        opponent.Session.Send(TradeOfferCodec.BuildUpdate(mutatingSide));
        return true;
    }
}
