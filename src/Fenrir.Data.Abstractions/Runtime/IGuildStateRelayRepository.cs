namespace Fenrir.Data.Abstractions.Runtime;

public interface IGuildStateRelayRepository
    : IClusterRelayBackend<GuildStateRelayEntry, GuildStateRelayDto>
{
}
