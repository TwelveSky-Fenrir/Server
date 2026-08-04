using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

[GenerateDto]
public sealed partial record RelaySourceIdentityDto(
    int CharacterId,
    int AccountId,
    string Name,
    byte Tribe,
    short AccountGrade);

public interface IRelaySourceIdentityRepository
{
    public ValueTask<RelaySourceIdentityDto?> GetAsync(int characterId, CancellationToken ct);
}
