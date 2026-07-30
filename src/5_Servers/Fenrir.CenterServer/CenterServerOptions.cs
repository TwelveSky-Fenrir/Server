namespace Fenrir.CenterServer;

public sealed class CenterServerOptions
{
    public const string SectionName = "Center";

    public int Port { get; set; } = 12003;

    public string BindAddress { get; set; } = "127.0.0.1";

    public string? SharedSecret { get; set; }
}
