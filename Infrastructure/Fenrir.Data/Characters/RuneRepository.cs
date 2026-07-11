using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

// Durable per-character rune-socket state (game.CharacterRunes) -- see IRuneRepository for the per-method
// contract. A rare transactional store, deliberately off ICharacterRepository's hot write-behind path.
public sealed record RuneRepository(ICaeriusNetDbContext Db) : IRuneRepository
{
    // usp_Character_PersistRunes' @Container sentinel: 255 is never a valid inventory container id, so it means
    // "do not touch any inventory container" (a TINYINT param can't be null-omitted the way a TVP can) -- same
    // convention as CharacterRepository.NoContainer.
    private const byte NoContainer = 255;

    public async ValueTask<ReadOnlyCollection<CharacterRuneSocketDto>> GetRunesAsync(int characterId,
        CancellationToken ct)
    {
        // Capacity 4 = aRuneSystem[4], the fixed per-character socket count.
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetRunes", 4)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<CharacterRuneSocketDto>(sp, ct);
    }

    public async ValueTask PersistRunesAsync(int characterId, IReadOnlyList<CharacterRuneSocketTvp> runes,
        byte? container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        // Scalars first (CharacterId, Container), then the two TVPs in the procedure's own parameter order.
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_PersistRunes", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container ?? NoContainer, SqlDbType.TinyInt);

        // Empty-TVP omission (SQL Server rejects a zero-row TVP): an all-empty rune state clears the table via
        // the procedure's unconditional DELETE, and a cleared inventory container likewise clears via its own.
        if (runes.Count > 0)
            builder.AddTvpParameter("Runes", runes);

        if (container is not null && items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }
}
