namespace Fenrir.Cluster.WorldState;

public readonly record struct CenterTribeStatAggregate(byte TribeId, long StatSum);

public interface ICenterTribeScoreSource
{
    public ValueTask<IReadOnlyList<CenterTribeStatAggregate>> ComputeAsync(CancellationToken ct);
}
