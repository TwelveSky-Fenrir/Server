namespace Fenrir.Data.Abstractions.Runtime;

public interface IPartyResyncRelayRepository
    : IAcknowledgedClusterRelayBackend<PartyResyncRelayEntry, PartyResyncRelayDto>
{
}
