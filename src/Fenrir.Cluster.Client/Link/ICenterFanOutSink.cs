namespace Fenrir.Cluster.Client.Link;

public interface ICenterFanOutSink
{
    public void Receive(byte opcode, ReadOnlySpan<byte> payload);
}
