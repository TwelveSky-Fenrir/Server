using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Data.Tribes;

// Faction-transfer scroll conversion (world.Items 8153/8154): atomic best-effort equip/skill/hotkey remap +
// tribe flip + scroll consumption in one procedure. See game.usp_Character_ApplyTribeScrollConversion's header.
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

        // Empty-TVP-omission rule: SQL Server rejects a zero-row TVP outright -- omit the parameter entirely
        // when the post-consumption container snapshot is empty (a legitimate "the scroll was the last item in
        // its container" clear), never pass an empty table.
        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }
}
