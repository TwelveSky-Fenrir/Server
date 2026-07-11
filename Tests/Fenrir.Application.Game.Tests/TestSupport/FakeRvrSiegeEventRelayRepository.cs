using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeRvrSiegeEventRelayRepository : IRvrSiegeEventRelayRepository
{
    public List<RvrSiegeEventRelayEntry> Published { get; } = [];

        public List<RvrSiegeEventRelayDto> NextPoll { get; set; } = [];

        public Exception? ThrowOnPublish { get; set; }

    public ValueTask PublishAsync(RvrSiegeEventRelayEntry entry, CancellationToken ct)
    {
        if (ThrowOnPublish is { } ex)
            throw ex;

        Published.Add(entry);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<RvrSiegeEventRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        var result = NextPoll.ToImmutableArray();
        NextPoll = [];
        return ValueTask.FromResult(result);
    }
}
