namespace Fenrir.CenterServer;

public sealed class CenterServerOptions
{

        public const string SectionName = "Center";

        public int Port { get; set; } = 12003;

        public string? SharedSecret { get; set; }
}
