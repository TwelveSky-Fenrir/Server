namespace Fenrir.Cluster.EventBus;

public interface ICenterCloseProxyRelay
{
    public ValueTask SendCloseProxyAsync(int zoneNumber, int userIndex, int characterIndex, int openUi,
        CancellationToken cancellationToken);
}
