namespace Fenrir.Data.Abstractions.Security;

public interface IBanRepository
{
    public ValueTask<bool> IsActiveForAccountAsync(int accountId, CancellationToken ct);

    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<int> CreateAsync(BanCreationRequest request, CancellationToken ct);
}
