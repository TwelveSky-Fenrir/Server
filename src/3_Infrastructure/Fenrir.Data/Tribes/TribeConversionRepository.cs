using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Data.Tribes;

public sealed record TribeConversionRepository(ICaeriusNetDbContext Db) : ITribeConversionRepository
{
    public async ValueTask ApplyTribeScrollConversionAsync(int characterId, int itemId, byte toTribe,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_ApplyTribeScrollConversion", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ItemId", itemId, SqlDbType.Int)
            .AddParameter("ToTribe", toTribe, SqlDbType.TinyInt)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }
}
