using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeSocialCrossShardRelayHandler(SocialCrossShardRelayKind kind) : ISocialCrossShardRelayHandler
{
    public List<SocialCrossShardRelayDto> AsksHandled { get; } = [];
    public List<SocialCrossShardRelayDto> AnswersHandled { get; } = [];
    public SocialCrossShardRelayKind Kind { get; } = kind;

    public ValueTask HandleAskAsync(SocialCrossShardRelayDto ask, CancellationToken ct)
    {
        AsksHandled.Add(ask);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAnswerAsync(SocialCrossShardRelayDto answer, CancellationToken ct)
    {
        AnswersHandled.Add(answer);
        return ValueTask.CompletedTask;
    }
}
