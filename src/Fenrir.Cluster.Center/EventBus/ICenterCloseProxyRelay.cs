namespace Fenrir.Cluster.Center.EventBus;

public interface ICenterCloseProxyRelay
{
    public ValueTask SendCloseProxyAsync(int zoneNumber, int userIndex, int characterIndex, int openUi,
        CancellationToken cancellationToken);
}
