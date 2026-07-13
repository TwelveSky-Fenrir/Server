namespace Fenrir.Domain.Game.Abstractions;

public interface IGameClock
{

        long UtcMilliseconds { get; }
}
