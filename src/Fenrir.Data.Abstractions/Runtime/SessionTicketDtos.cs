using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public readonly record struct MintedSessionTicketDto(string Capability);

[GenerateDto]
public sealed partial record ConsumedTicketDto(
    int AccountId,
    int CharacterId,
    byte ShardId,
    Guid SessionToken,
    short AccountGrade = 0,
    short TargetMapId = 0);
