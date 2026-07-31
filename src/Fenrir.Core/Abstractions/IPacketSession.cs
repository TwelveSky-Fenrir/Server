using System.Net;

namespace Fenrir.Core.Abstractions;

public interface IPacketSession
{
    public long SessionId { get; }

    public IPEndPoint? RemoteEndPoint { get; }

    public DateTimeOffset LastActivityUtc { get; }

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;

    public void SendRaw(ReadOnlySpan<byte> rawFrame);

    public void Abort(DisconnectReason reason);
}
