namespace Fenrir.Cluster.Link;

public interface ICenterFanOutSink
{
    public void Receive(byte opcode, ReadOnlySpan<byte> payload);
}
