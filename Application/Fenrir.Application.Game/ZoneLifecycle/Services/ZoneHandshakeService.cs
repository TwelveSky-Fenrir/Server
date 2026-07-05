using Fenrir.Application.Game.Handlers;
using Fenrir.Data.Runtime;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

public enum ZoneHandshakeOutcome
{
    Rejected,
    Accepted
}

public readonly record struct ZoneHandshakeResult(ZoneHandshakeOutcome Outcome, int AccountId = 0, int CharacterId = 0);

/// <summary>
///     Business logic for op11, the first packet after ZC_CONNECT_OK_RECV: consumes the single-use session
///     ticket the LoginServer minted for this AccountId (ADR-0005) -- see <c>ZoneHandshakeHandler</c>'s remarks.
/// </summary>
public interface IZoneHandshakeService
{
    ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, CancellationToken cancellationToken);
}

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
