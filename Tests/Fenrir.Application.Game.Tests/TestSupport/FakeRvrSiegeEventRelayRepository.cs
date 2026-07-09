using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="IRvrSiegeEventRelayRepository" /> -- records every published entry
///     (append-only) and returns a scripted set of rows from <see cref="PollAsync" />, so
///     <c>RvrSiegeEventRelayHost</c>'s own poll-loop tests can assert publish/deliver behavior without a real
///     SQL round trip.
/// </summary>
internal sealed class FakeRvrSiegeEventRelayRepository : IRvrSiegeEventRelayRepository
{
    public List<RvrSiegeEventRelayEntry> Published { get; } = [];

    /// <summary>Rows <see cref="PollAsync" /> returns on its NEXT call, then clears (single-shot, like a real poll cursor advancing).</summary>
    public List<RvrSiegeEventRelayDto> NextPoll { get; set; } = [];

    /// <summary>When set, <see cref="PublishAsync" /> throws this instead of recording the entry.</summary>
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
