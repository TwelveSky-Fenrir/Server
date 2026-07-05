using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Discriminator for how a CZ_TRADE_ASK_SEND attempt resolved.</summary>
public enum TradeInviteResultKind
{
    TargetNotFound,
    MustDisconnect,
    AskerBusy,
    TargetBusy,
    Sent
}

public readonly record struct TradeInviteResult(
    TradeInviteResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? AskerName = null,
    int AskerLevel = 0);

public interface ITradeInviteService
{
    TradeInviteResult Invite(Zone zone, PlayerRuntimeState asker, string targetAvatarName);
}

/// <summary>
///     Level uses <see cref="PlayerRuntimeState.Level" /> alone; aLevel2 isn't modeled, same gap as party's
///     invite check.
/// </summary>
public sealed class TradeInviteService(TradeRegistry trades) : ITradeInviteService
{
    public TradeInviteResult Invite(Zone zone, PlayerRuntimeState asker, string targetAvatarName)
    {
        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
            return new TradeInviteResult(TradeInviteResultKind.TargetNotFound);

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && asker.Tribe != target.Tribe)
            return new TradeInviteResult(TradeInviteResultKind.MustDisconnect);

        return trades.TryAsk(asker.CharacterId, target.CharacterId) switch
        {
            TradeAskOutcome.AskerBusy => new TradeInviteResult(TradeInviteResultKind.AskerBusy),
            TradeAskOutcome.TargetBusy => new TradeInviteResult(TradeInviteResultKind.TargetBusy),
            _ => new TradeInviteResult(TradeInviteResultKind.Sent, target.CharacterId, target.Name, asker.Name,
                asker.Level)
        };
    }
}
