namespace Fenrir.Data.Abstractions.Runtime;

public interface IRvrSiegeEventRelayRepository
    : IAcknowledgedClusterRelayBackend<RvrSiegeEventRelayEntry, RvrSiegeEventRelayDto>
{
}
