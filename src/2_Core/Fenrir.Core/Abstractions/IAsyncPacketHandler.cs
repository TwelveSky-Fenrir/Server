namespace Fenrir.Core.Abstractions;

/// <summary>
/// Handler <b>asynchrone</b> (I/O réelle : DB, cross-serveur). Le paquet est passé par valeur (déjà matérialisé),
/// sûr à travers un <c>await</c>. Le générateur Dispatch émet <c>MessageDispatcher.TryHandleAsync</c>.
/// </summary>
public interface IAsyncPacketHandler<in TPacket>
    where TPacket : struct, IIncomingPacket<TPacket>
{
    public ValueTask HandleAsync(TPacket packet, IPacketSession session, CancellationToken cancellationToken);
}
