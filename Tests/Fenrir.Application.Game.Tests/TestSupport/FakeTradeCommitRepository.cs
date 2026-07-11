using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     In-memory stand-in for <see cref="ITradeCommitRepository" /> -- records every call verbatim (including
///     the idempotency token) so C8-trade-finalize tests can assert <c>TradeLockService.CommitAsync</c> commits
///     through the anti-dupe repository (never the plain <c>ICharacterRepository.ExecuteTradeAsync</c>) and that
///     a fresh <see cref="Guid" /> token is supplied on every distinct commit attempt.
/// </summary>
internal sealed class FakeTradeCommitRepository : ITradeCommitRepository
{
    public List<TradeCommitCall> Calls { get; } = [];

    /// <summary>Set to make the next (and only the next) call throw, simulating a SQL 50268/50269 rejection.</summary>
    public Exception? ThrowOnNextExecute { get; set; }

    public ValueTask ExecuteIdempotentAsync(
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
        if (ThrowOnNextExecute is { } ex)
        {
            ThrowOnNextExecute = null;
            throw ex;
        }

        Calls.Add(new TradeCommitCall(tradeToken, characterA, itemsA0, itemsA1, deltaMoneyA, deltaBigMoneyA,
            characterB, itemsB0, itemsB1, deltaMoneyB, deltaBigMoneyB, tradedItemsA, tradedItemsB, offeredMoneyA,
            offeredBigMoneyA, offeredMoneyB, offeredBigMoneyB));
        return ValueTask.CompletedTask;
    }

    public readonly record struct TradeCommitCall(
        Guid TradeToken,
        int CharacterA,
        IReadOnlyList<CharacterItemSlotTvp> ItemsA0,
        IReadOnlyList<CharacterItemSlotTvp> ItemsA1,
        long DeltaMoneyA,
        int DeltaBigMoneyA,
        int CharacterB,
        IReadOnlyList<CharacterItemSlotTvp> ItemsB0,
        IReadOnlyList<CharacterItemSlotTvp> ItemsB1,
        long DeltaMoneyB,
        int DeltaBigMoneyB,
        IReadOnlyList<CharacterItemSlotTvp>? TradedItemsA,
        IReadOnlyList<CharacterItemSlotTvp>? TradedItemsB,
        long OfferedMoneyA,
        int OfferedBigMoneyA,
        long OfferedMoneyB,
        int OfferedBigMoneyB);
}
