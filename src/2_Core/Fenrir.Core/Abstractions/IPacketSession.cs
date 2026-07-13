namespace Fenrir.Core.Abstractions;

/// <summary>
/// Vue serveur-agnostique d'une session cliente exposée aux handlers : identité et envoi typé sortant. Surface
/// 100 % Core+BCL (remontée dans Core pour éviter une inversion Core→Infrastructure via les interfaces handler).
/// </summary>
public interface IPacketSession
{
    public long SessionId { get; }

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;
}
