namespace Fenrir.Cluster.Link;

public interface ICenterLink
{
    public bool IsConnected { get; }

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;
}
