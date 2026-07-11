using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakePartyResyncRelayRepository : IPartyResyncRelayRepository
{
    public List<PartyResyncRelayEntry> Published { get; } = [];

    public List<PartyResyncRelayDto> NextPoll { get; set; } = [];

    public Exception? ThrowOnPublish { get; set; }

    public ValueTask PublishAsync(PartyResyncRelayEntry entry, CancellationToken ct)
    {
        if (ThrowOnPublish is { } ex)
            throw ex;

        Published.Add(entry);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<PartyResyncRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct)
    {
        var result = NextPoll.ToImmutableArray();
        NextPoll = [];
        return ValueTask.FromResult(result);
    }
}
