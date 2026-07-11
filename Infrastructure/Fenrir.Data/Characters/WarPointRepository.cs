using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record WarPointRepository(ICaeriusNetDbContext Db) : IWarPointRepository
{

        private const int InsufficientSentinel = -1;

    public async ValueTask<WarPointPurchaseResult> BuyWarPointItemAsync(int characterId, int warPointCost,
        byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Character_BuyWarPointItem", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("WarPointCost", warPointCost, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        var newBalance = await Db.ExecuteScalarAsync<int>(builder.Build(), ct);

        return newBalance <= InsufficientSentinel
            ? new WarPointPurchaseResult(false, 0)
            : new WarPointPurchaseResult(true, newBalance);
    }
}
