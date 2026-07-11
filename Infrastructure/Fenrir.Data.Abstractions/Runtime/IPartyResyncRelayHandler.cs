namespace Fenrir.Data.Abstractions.Runtime;

public interface IPartyResyncRelayHandler
{

        public ValueTask HandleAsync(PartyResyncRelayDto row, CancellationToken ct);
}
