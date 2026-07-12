using System.Collections.Immutable;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Data.Tribes;

public sealed record TribeRosterRepository(ICaeriusNetDbContext Db) : ITribeRosterRepository
{
    public async ValueTask<ImmutableArray<TribeRosterCharacterDto>> GetForTribePointAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeRoster_GetForTribePoint", 64).Build();

        return await Db.QueryAsImmutableArrayAsync<TribeRosterCharacterDto>(sp, ct);
    }
}
