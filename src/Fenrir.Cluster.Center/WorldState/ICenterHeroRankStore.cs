namespace Fenrir.Cluster.Center.WorldState;

public readonly record struct CenterHeroRankAccrual(int CharacterId, byte TribeId, int Points, int Level);

public interface ICenterHeroRankStore
{
    public ValueTask<IReadOnlyList<CenterHeroRankAccrual>> LoadCurrentPeriodAsync(CancellationToken ct);

    public ValueTask UpsertCurrentPeriodAsync(int characterId, int points, byte tribeId, int level,
        CancellationToken ct);
}
