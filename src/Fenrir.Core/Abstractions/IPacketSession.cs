using System.Net;

namespace Fenrir.Core.Abstractions;

public interface IPacketSession
{
    public long SessionId { get; }

    public IPEndPoint? RemoteEndPoint { get; }

    public DateTimeOffset LastActivityUtc { get; }

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;

    public bool TrySend<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        Send(in packet);
        return true;
    }

    public void SendRaw(ReadOnlySpan<byte> rawFrame);

    public bool TrySendRaw(ReadOnlySpan<byte> rawFrame)
    {
        SendRaw(rawFrame);
        return true;
    }

    public void Abort(DisconnectReason reason);
}
