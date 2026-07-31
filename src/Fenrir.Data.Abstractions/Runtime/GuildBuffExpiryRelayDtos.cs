using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public sealed record GuildBuffExpiryRelayEntry(byte SourceShardId, int GuildId, int NewBuffTime)
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

[GenerateDto]
public sealed partial record GuildBuffExpiryRelayDto(long RelayId, int GuildId, int NewBuffTime);
