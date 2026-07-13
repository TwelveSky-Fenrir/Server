namespace Fenrir.Data.Abstractions.Runtime;

public interface IPartyResyncRelayRepository
    : IClusterRelayBackend<PartyResyncRelayEntry, PartyResyncRelayDto>
{
}
