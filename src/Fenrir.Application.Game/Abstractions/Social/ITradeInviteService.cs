using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public enum TradeInviteResultKind
{
    TargetNotFound,
    MustDisconnect,
    AskerBusy,
    TargetBusy,
    Sent,

    SentCrossShard
}

public readonly record struct TradeInviteResult(
    TradeInviteResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? AskerName = null,
    int AskerLevel = 0);

public interface ITradeInviteService
{
    public ValueTask<TradeInviteResult> InviteAsync(Zone zone, PlayerRuntimeState asker, string targetAvatarName,
        CancellationToken cancellationToken);
}
