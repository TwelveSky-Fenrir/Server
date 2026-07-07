using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

public sealed record MuteRepository(ICaeriusNetDbContext Db) : IMuteRepository
{
    /// <summary>
    ///     Called once at world entry to seed PlayerRuntimeState.IsMuted -- see GetActiveCharacterIdsAsync for the
    ///     periodic re-check that follows it.
    /// </summary>
    public async ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Mute_GetActiveForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<MuteRowDto>(sp, ct);
        return rows.Count > 0;
    }

    /// <summary>
    ///     Never called with an empty characterIds collection by contract; guarded here anyway since SQL Server rejects
    ///     an empty TVP outright.
    /// </summary>
    public async ValueTask<ImmutableArray<int>> GetActiveCharacterIdsAsync(IReadOnlyCollection<int> characterIds,
        CancellationToken ct)
    {
        if (characterIds.Count == 0)
            return ImmutableArray<int>.Empty;

        var rows = characterIds.Select(id => new CharacterIdTvp(id)).ToArray();

        var sp = new StoredProcedureParametersBuilder("admin", "usp_Mute_GetActiveForCharacters", characterIds.Count)
            .AddTvpParameter("CharacterIds", rows)
            .Build();

        var muted = await Db.QueryAsImmutableArrayAsync<MutedCharacterIdDto>(sp, ct);
        return muted.IsEmpty ? ImmutableArray<int>.Empty : muted.Select(m => m.CharacterId).ToImmutableArray();
    }
}
