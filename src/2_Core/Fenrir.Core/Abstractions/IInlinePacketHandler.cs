namespace Fenrir.Core.Abstractions;

/// <summary>
/// Handler <b>synchrone</b> (budget microseconde, état en mémoire uniquement, aucune I/O). Le générateur Dispatch
/// le découvre par implémentation et émet <c>MessageDispatcher.TryHandleInline</c>.
/// </summary>
public interface IInlinePacketHandler<TPacket>
    where TPacket : struct, IIncomingPacket<TPacket>
{
    public void Handle(in TPacket packet, IPacketSession session);
}
