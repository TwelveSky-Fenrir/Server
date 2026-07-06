using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneHandshakeService(
    ISessionTicketRepository tickets,
    IAccountSessionRepository accountSessions,
    IOptions<GameServerOptions> options) : IZoneHandshakeService
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

        // Cross-process duplicate-login authority: proves this world-entry claim is for the same login epoch that
        // minted the ticket, not a hijack of a newer login (runtime.AccountSessions moved on since this ticket was
        // issued -- e.g. the account logged in again elsewhere before this ticket got consumed).
        var transitioned = await accountSessions
            .TransitionToGameAsync(accountId, consumed.SessionToken, options.Value.ShardId, cancellationToken)
            .ConfigureAwait(false);

        if (!transitioned)
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.SessionSuperseded);

        return new ZoneHandshakeResult(ZoneHandshakeOutcome.Accepted, accountId, consumed.CharacterId,
            consumed.SessionToken);
    }
}
