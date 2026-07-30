namespace Fenrir.Domain.Game.Abstractions;

public interface IGameClock
{
    public long UtcMilliseconds { get; }
}
