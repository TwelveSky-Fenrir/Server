using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Abstractions.World;

public interface ICharacterWriteBehindFlusher : IWriteBehindFlusher
{
    public ValueTask<bool> FlushCharacterNowAsync(int characterId, CancellationToken ct, bool queueOnFailure = true);

    public ValueTask<CharacterSnapshotPersistence> FlushCharacterSnapshotAsync(PlayerRuntimeState snapshot,
        CancellationToken ct);
}

public readonly record struct CharacterSnapshotPersistence(Task Completion)
{
    public bool IsDurablyPersisted => Completion.IsCompletedSuccessfully;

    public static CharacterSnapshotPersistence Persisted { get; } = new(Task.CompletedTask);
}
