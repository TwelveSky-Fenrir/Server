namespace Fenrir.Data.Abstractions.Runtime;

public interface IChatCrossShardRelayRepository
    : IClusterRelayBackend<ChatCrossShardWhisperEntry, ChatCrossShardWhisperDto>
{
}
