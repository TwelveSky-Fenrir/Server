using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public interface IPartyResyncRelayRepository
{

        public ValueTask PublishAsync(PartyResyncRelayEntry entry, CancellationToken ct);

        public ValueTask<ImmutableArray<PartyResyncRelayDto>> PollAsync(byte shardId, int retentionSeconds,
        CancellationToken ct);
}
