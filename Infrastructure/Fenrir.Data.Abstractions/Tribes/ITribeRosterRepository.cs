using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Tribes;

public interface ITribeRosterRepository
{
    public ValueTask<ReadOnlyCollection<TribeRosterCharacterDto>> GetForTribePointAsync(CancellationToken ct);
}
