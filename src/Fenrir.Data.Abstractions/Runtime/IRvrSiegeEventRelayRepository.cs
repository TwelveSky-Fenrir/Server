namespace Fenrir.Data.Abstractions.Runtime;

public interface IRvrSiegeEventRelayRepository
    : IClusterRelayBackend<RvrSiegeEventRelayEntry, RvrSiegeEventRelayDto>
{
}
