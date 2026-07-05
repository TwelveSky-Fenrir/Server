namespace Fenrir.Data.Abstractions.Admin;

public interface IMuteRepository
{
    public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);
}
