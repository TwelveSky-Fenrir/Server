using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

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
    public TradeInviteResult Invite(Zone zone, PlayerRuntimeState asker, string targetAvatarName);
}
