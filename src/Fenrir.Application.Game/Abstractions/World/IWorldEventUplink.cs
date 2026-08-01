namespace Fenrir.Application.Game.Abstractions.World;

public interface IWorldEventUplink
{
    public void Publish(int sort, ReadOnlySpan<byte> data);
}
