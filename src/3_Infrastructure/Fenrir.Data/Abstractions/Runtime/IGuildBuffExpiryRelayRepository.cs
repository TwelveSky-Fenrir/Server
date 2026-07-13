namespace Fenrir.Data.Abstractions.Runtime;

public interface IGuildBuffExpiryRelayRepository
    : IClusterRelayBackend<GuildBuffExpiryRelayEntry, GuildBuffExpiryRelayDto>
{
}
