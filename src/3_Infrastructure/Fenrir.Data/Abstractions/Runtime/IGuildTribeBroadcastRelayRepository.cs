namespace Fenrir.Data.Abstractions.Runtime;

public interface IGuildTribeBroadcastRelayRepository
    : IClusterRelayBackend<GuildTribeBroadcastRelayEntry, GuildTribeBroadcastRelayDto>
{
}
