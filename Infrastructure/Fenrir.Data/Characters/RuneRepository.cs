using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record RuneRepository(ICaeriusNetDbContext Db) : IRuneRepository
{
    private const byte NoContainer = 255;

    public async ValueTask<ReadOnlyCollection<CharacterRuneSocketDto>> GetRunesAsync(int characterId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetRunes", 4)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<CharacterRuneSocketDto>(sp, ct);
    }

    public async ValueTask PersistRunesAsync(int characterId, IReadOnlyList<CharacterRuneSocketTvp> runes,
        byte? container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_PersistRunes", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container ?? NoContainer, SqlDbType.TinyInt);

        if (runes.Count > 0)
            builder.AddTvpParameter("Runes", runes);

        if (container is not null && items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }
}
