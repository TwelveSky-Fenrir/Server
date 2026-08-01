namespace Fenrir.Data.Abstractions.Runtime;

public interface ISocialCrossShardRelayRepository
    : IClusterRelayBackend<SocialCrossShardRelayEntry, SocialCrossShardRelayDto>
{
}
