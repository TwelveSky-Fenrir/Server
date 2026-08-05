namespace Fenrir.Application.Game.Abstractions.Gm;

public enum GameDataReloadScope : byte
{
    All,
    Monsters,
    Items,
    Quests
}

public readonly record struct GameDataReloadOutcome(
    bool Succeeded,
    GameDataReloadScope Scope,
    int ItemCount,
    int MonsterCount,
    int QuestCount,
    TimeSpan Elapsed,
    string? Failure);

public interface IGameDataReloadService
{
    ValueTask<GameDataReloadOutcome> ReloadAsync(GameDataReloadScope scope, CancellationToken cancellationToken);
}
