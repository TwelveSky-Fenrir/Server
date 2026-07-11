using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeSocialCrossShardRelayRepository : ISocialCrossShardRelayRepository
{
    public List<SocialCrossShardRelayEntry> Published { get; } = [];

    public List<SocialCrossShardRelayDto> NextPoll { get; set; } = [];

    public Exception? ThrowOnPublish { get; set; }

    public ValueTask PublishAsync(SocialCrossShardRelayEntry entry, CancellationToken ct)
    {
        if (ThrowOnPublish is { } ex)
            throw ex;

        Published.Add(entry);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<SocialCrossShardRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        var result = NextPoll.ToImmutableArray();
        NextPoll = [];
        return ValueTask.FromResult(result);
    }
}
