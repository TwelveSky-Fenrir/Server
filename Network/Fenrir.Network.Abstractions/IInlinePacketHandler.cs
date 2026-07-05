namespace Fenrir.Network.Abstractions;

/// <summary>Synchronous handler run on the session loop: microsecond budget, never SQL or a contended lock.</summary>
public interface IInlinePacketHandler<TPacket>
    where TPacket : struct, IIncomingPacket<TPacket>
{
    public void Handle(in TPacket packet, IPacketSession session);
}
