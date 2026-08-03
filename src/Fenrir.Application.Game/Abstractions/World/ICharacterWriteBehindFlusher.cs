using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Abstractions.World;

public interface ICharacterWriteBehindFlusher : IWriteBehindFlusher
{
    public ValueTask FlushCharacterNowAsync(int characterId, CancellationToken ct);

    public ValueTask FlushCharacterSnapshotAsync(PlayerRuntimeState snapshot, CancellationToken ct);
}
