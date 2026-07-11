namespace Fenrir.Data.Abstractions.Game;

public interface IEventLogQueue
{
    public bool Enqueue(EventLogEntryTvp entry);
}
