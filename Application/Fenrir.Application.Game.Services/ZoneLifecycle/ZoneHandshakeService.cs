using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneHandshakeService(ISessionTicketRepository tickets, IOptions<GameServerOptions> options)
    : IZoneHandshakeService
{
    public async ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId,
        CancellationToken cancellationToken)
    {
        if (!ObfuscatedUidCodec.TryDecodeAccountId(obfuscatedId, out var accountId))
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);

        var consumed = await tickets.ConsumeAsync(accountId, cancellationToken);

        // Refuse absent/expired/wrong-shard tickets identically (Result=1) so we don't leak which failure occurred.
        if (consumed is null || consumed.ShardId != options.Value.ShardId)
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);

        return new ZoneHandshakeResult(ZoneHandshakeOutcome.Accepted, accountId, consumed.CharacterId);
    }
}
