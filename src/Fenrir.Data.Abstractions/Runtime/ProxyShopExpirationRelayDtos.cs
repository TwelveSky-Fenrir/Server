using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public sealed record ProxyShopExpirationRelayEntry(
    byte SourceShardId,
    int CharacterId,
    int NewExpirationDate)
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

[GenerateDto]
public sealed partial record ProxyShopExpirationRelayDto(
    long RelayId,
    int CharacterId,
    int NewExpirationDate);
