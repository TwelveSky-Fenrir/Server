using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

/// <summary>
///     CaeriusNet implementation of <see cref="IWarPointRepository" />. The atomic purchase procedure returns a
///     single scalar: the new War-Point balance on success, or the sentinel <c>-1</c> when the balance was
///     insufficient (no state changed) -- a normal result set, not an exception, so an ordinary "can't afford"
///     outcome never surfaces as a fault the caller would have to catch and disambiguate from a real DB error.
/// </summary>
public sealed record WarPointRepository(ICaeriusNetDbContext Db) : IWarPointRepository
{
    /// <summary>Sentinel returned by usp_Character_BuyWarPointItem when the War-Point balance is insufficient.</summary>
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
