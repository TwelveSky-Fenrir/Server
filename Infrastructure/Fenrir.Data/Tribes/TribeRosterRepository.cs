using System.Collections.ObjectModel;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Data.Tribes;

// Roster read behind the level/rebirth-based tribe-point recompute (C17 Part C). Sourced from game.Characters
// (the genuine avatar roster), Level >= 145 only -- see game.usp_TribeRoster_GetForTribePoint's header for why
// this deliberately does NOT reproduce the legacy's RankInfo-table query bug.
public sealed record TribeRosterRepository(ICaeriusNetDbContext Db) : ITribeRosterRepository
{
    public async ValueTask<ReadOnlyCollection<TribeRosterCharacterDto>> GetForTribePointAsync(CancellationToken ct)
    {
        // ResultSetCapacity 64: no fixed bound on the max-level population, so the conventional "wide,
        // unbounded" default rather than a guessed constant.
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeRoster_GetForTribePoint", 64).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeRosterCharacterDto>(sp, ct);
    }
}
