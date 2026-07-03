namespace Fenrir.Contracts.Abstractions;

/// <summary>
///     Core abstraction over the network session (implemented by <c>ClientSession</c> in
///     Fenrir.Network/Infrastructure), so handlers can reference a session without a Core → Infrastructure dependency.
/// </summary>
public interface IPacketSession
{
    public long SessionId { get; }

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;
}
