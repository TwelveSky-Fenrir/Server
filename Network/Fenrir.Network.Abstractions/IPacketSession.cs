namespace Fenrir.Network.Abstractions;

/// <summary>Session abstraction so handlers avoid a Core → Infrastructure dependency on <c>ClientSession</c>.</summary>
public interface IPacketSession
{
    public long SessionId { get; }

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;
}
