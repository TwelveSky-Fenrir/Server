using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.World;

public interface IWorldEventSnapshotRepository
{
    public ValueTask<ReadOnlyCollection<WorldEventSnapshotRowDto>> LoadAllAsync(CancellationToken ct);

    public ValueTask<bool> TryApplyAsync(string eventKind, string occurrenceKey, long expectedRevision, string phase,
        string canonicalPayload, byte[] canonicalPayloadHash, CancellationToken ct);
}
