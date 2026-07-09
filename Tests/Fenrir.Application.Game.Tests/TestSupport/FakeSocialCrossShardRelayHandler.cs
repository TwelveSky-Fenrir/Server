using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for a single <see cref="SocialCrossShardRelayKind" />'s own
///     <see cref="ISocialCrossShardRelayHandler" /> -- records every Ask/Answer row routed to it so
///     <c>SocialCrossShardRelayHost</c>'s own delivery-routing tests can assert the right handler (and only
///     that one) received a given row, without any of the six real negotiation registries.
/// </summary>
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
