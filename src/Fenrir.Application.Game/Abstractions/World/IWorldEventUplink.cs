namespace Fenrir.Application.Game.Abstractions.World;

public interface IWorldEventUplink
{
    public WorldEventUplinkResult Publish(int sort, ReadOnlySpan<byte> data,
        WorldEventPublicationIdentity? identity = null);
}
