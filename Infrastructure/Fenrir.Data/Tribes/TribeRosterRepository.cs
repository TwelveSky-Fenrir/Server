using System.Collections.Immutable;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Data.Tribes;

public sealed record TribeRosterRepository(ICaeriusNetDbContext Db) : ITribeRosterRepository
{
    // Polled every ~6 ticks by TribePointRecomputeHost (see usp_TribeRoster_GetForTribePoint.sql's own
    // header comment) -- QueryAsImmutableArrayAsync is the polled/hot-path terminal call.
    public async ValueTask<ImmutableArray<TribeRosterCharacterDto>> GetForTribePointAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeRoster_GetForTribePoint", 64).Build();

        return await Db.QueryAsImmutableArrayAsync<TribeRosterCharacterDto>(sp, ct);
    }
}
