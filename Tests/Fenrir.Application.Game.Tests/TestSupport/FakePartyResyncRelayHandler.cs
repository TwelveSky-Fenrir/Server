using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for the party-reconciliation authority's own <see cref="IPartyResyncRelayHandler" />
///     -- records every row routed to it so <c>PartyResyncRelayHost</c>'s own delivery-routing tests can assert
///     the handler received a given row, without the real per-shard <c>PartyRegistry</c> reconciliation.
/// </summary>
internal sealed class FakePartyResyncRelayHandler : IPartyResyncRelayHandler
{
    public List<PartyResyncRelayDto> Handled { get; } = [];

    public ValueTask HandleAsync(PartyResyncRelayDto row, CancellationToken ct)
    {
        Handled.Add(row);
        return ValueTask.CompletedTask;
    }
}
