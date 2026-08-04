namespace Fenrir.Data.Abstractions.Runtime;

public interface ISocialCrossShardRelayHandler
{
    public SocialCrossShardRelayKind Kind { get; }

    public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct);

    public ValueTask HandleAnswerAsync(SocialCrossShardRelayDto answer, CancellationToken ct);

    public ValueTask HandleCancelAsync(SocialCrossShardRelayDto cancel, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}
