namespace Fenrir.Data.Admin;

/// <summary>Abstraction over Fenrir.Data.Admin.MuteRepository for DI/testability.</summary>
public interface IMuteRepository
{
    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);
}
