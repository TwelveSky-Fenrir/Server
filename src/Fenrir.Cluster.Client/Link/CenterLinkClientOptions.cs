namespace Fenrir.Cluster.Client.Link;

public sealed class CenterLinkClientOptions
{
    public const string SectionName = "Center";

    public string? Endpoint { get; set; }

    public string? SharedSecret { get; set; }

    public int ConnectTimeoutMilliseconds { get; set; } = 5_000;

    public int InitialReconnectDelayMilliseconds { get; set; } = 1_000;

    public int MaxReconnectDelayMilliseconds { get; set; } = 30_000;
}
