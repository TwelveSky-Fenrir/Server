using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record TradeCommitRepository(ICaeriusNetDbContext Db) : ITradeCommitRepository
{
    public async ValueTask ExecuteIdempotentAsync(
        Guid tradeToken,
        int characterA, IReadOnlyList<CharacterItemSlotTvp> itemsA0, IReadOnlyList<CharacterItemSlotTvp> itemsA1,
        long deltaMoneyA, int deltaBigMoneyA,
        int characterB, IReadOnlyList<CharacterItemSlotTvp> itemsB0, IReadOnlyList<CharacterItemSlotTvp> itemsB1,
        long deltaMoneyB, int deltaBigMoneyB,
        CancellationToken ct,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsA = null,
        IReadOnlyList<CharacterItemSlotTvp>? tradedItemsB = null,
        long offeredMoneyA = 0, int offeredBigMoneyA = 0,
        long offeredMoneyB = 0, int offeredBigMoneyB = 0)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterTradeCommit_ExecuteIdempotent", 0)
            .AddParameter("TradeToken", tradeToken, SqlDbType.UniqueIdentifier)
            .AddParameter("CharacterA", characterA, SqlDbType.Int);

        if (itemsA0.Count > 0) builder.AddTvpParameter("ItemsA0", itemsA0);
        if (itemsA1.Count > 0) builder.AddTvpParameter("ItemsA1", itemsA1);

        builder.AddParameter("DeltaMoneyA", deltaMoneyA, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoneyA", deltaBigMoneyA, SqlDbType.Int)
            .AddParameter("CharacterB", characterB, SqlDbType.Int);

        if (itemsB0.Count > 0) builder.AddTvpParameter("ItemsB0", itemsB0);
        if (itemsB1.Count > 0) builder.AddTvpParameter("ItemsB1", itemsB1);

        builder.AddParameter("DeltaMoneyB", deltaMoneyB, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoneyB", deltaBigMoneyB, SqlDbType.Int);

        if (tradedItemsA is { Count: > 0 }) builder.AddTvpParameter("TradedItemsA", tradedItemsA);
        if (tradedItemsB is { Count: > 0 }) builder.AddTvpParameter("TradedItemsB", tradedItemsB);

        builder.AddParameter("OfferedMoneyA", offeredMoneyA, SqlDbType.BigInt)
            .AddParameter("OfferedBigMoneyA", offeredBigMoneyA, SqlDbType.Int)
            .AddParameter("OfferedMoneyB", offeredMoneyB, SqlDbType.BigInt)
            .AddParameter("OfferedBigMoneyB", offeredBigMoneyB, SqlDbType.Int);

        await Db.ExecuteAsync(builder.Build(), ct);
    }
}
