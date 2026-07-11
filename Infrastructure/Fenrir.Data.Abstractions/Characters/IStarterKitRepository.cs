namespace Fenrir.Data.Abstractions.Characters;

public interface IStarterKitRepository
{
    public ValueTask<StarterKitBundle> GetByPreviousTribeAsync(byte previousTribe, short mapId, CancellationToken ct);
}
