namespace Fenrir.Data.Abstractions.Runtime;

public interface IGuildTribeBroadcastRelayRepository
    : IAcknowledgedClusterRelayBackend<GuildTribeBroadcastRelayEntry, GuildTribeBroadcastRelayDto>
{
}
