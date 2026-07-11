using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakePartyResyncRelayHandler : IPartyResyncRelayHandler
{
    public List<PartyResyncRelayDto> Handled { get; } = [];

    public ValueTask HandleAsync(PartyResyncRelayDto row, CancellationToken ct)
    {
        Handled.Add(row);
        return ValueTask.CompletedTask;
    }
}
