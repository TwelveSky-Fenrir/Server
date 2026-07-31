namespace Fenrir.Cluster.Center.WorldState;

public interface IHeroRankAuthority
{
    public Task InitializeAsync(CancellationToken ct);

    public void AddOrUpdate(int hPoint, byte hTribe, int hLevel, int uCharIdx);

    public ValueTask FlushDirtyAsync(CancellationToken ct);
}
