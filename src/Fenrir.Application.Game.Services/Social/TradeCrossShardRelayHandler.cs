namespace Fenrir.Application.Game.Services.Social;

public sealed class TradeCrossShardRelayHandler : ISocialCrossShardRelayHandler
{
    public SocialCrossShardRelayKind Kind => SocialCrossShardRelayKind.Trade;

    public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAnswerAsync(SocialCrossShardRelayDto answer, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}
