namespace Fenrir.Contracts.Abstractions;

/// <summary>Asynchronous handler awaited by the session loop (auth, character selection, ticket consumption).</summary>
public interface IAsyncPacketHandler<in TPacket>
    where TPacket : struct, IIncomingPacket<TPacket>
{
    public ValueTask HandleAsync(TPacket packet, IPacketSession session, CancellationToken cancellationToken);
}
