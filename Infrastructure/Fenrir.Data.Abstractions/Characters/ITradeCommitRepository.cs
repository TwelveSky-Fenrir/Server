namespace Fenrir.Data.Abstractions.Characters;

/// <summary>
///     CaeriusNet wrapper around <c>game.usp_CharacterTradeCommit_ExecuteIdempotent</c> -- the C8 durable,
///     anti-dupe variant of <see cref="ICharacterRepository.ExecuteTradeAsync" />. A dedicated, single-method
///     repository, deliberately NOT added to <see cref="ICharacterRepository" />/<c>CharacterRepository</c>
///     (the hottest magnet files many concurrent passes touch) -- same precedent as
///     <c>IRuneRepository</c>/<c>RuneRepository</c>.
/// </summary>
/// <remarks>
///     Legacy performs the trade exchange purely in memory and defers durability to the periodic avatar save
///     (no crash/retry protection at all -- see the C8-trade behavior contract's own "Durability across a
///     crash" edge case). Fenrir's commit is instead a single durable SQL transaction
///     (<see cref="ICharacterRepository.ExecuteTradeAsync" />'s own doc already documents this as a deliberate
///     uplift), and this repository adds a second, narrower uplift on top: exactly-once commit semantics for a
///     retried confirm, via <c>game.TradeCommitLedger</c>. Both uplifts are Fenrir-only design decisions, not
///     legacy parity.
/// </remarks>
public interface ITradeCommitRepository
{
    /// <summary>
    ///     Atomic two-character trade commit, idempotent on <paramref name="tradeToken" /> -- a second call
    ///     carrying the same token is a benign no-op (the additive money legs are never re-applied). Every other
    ///     parameter has the exact same contract as
    ///     <see cref="ICharacterRepository.ExecuteTradeAsync" /> (whole-container replaces for
    ///     <paramref name="itemsA0" />/<paramref name="itemsA1" />/<paramref name="itemsB0" />/
    ///     <paramref name="itemsB1" />, net money deltas for <paramref name="deltaMoneyA" />/
    ///     <paramref name="deltaMoneyB" />, and the audit-only <paramref name="tradedItemsA" />/
    ///     <paramref name="tradedItemsB" />/<paramref name="offeredMoneyA" />/<paramref name="offeredBigMoneyA" />/
    ///     <paramref name="offeredMoneyB" />/<paramref name="offeredBigMoneyB" /> parameters) -- see that
    ///     method's own doc for the full per-parameter contract, which this call mirrors verbatim.
    /// </summary>
    /// <param name="tradeToken">
    ///     A per-commit idempotency token. The caller generates this ONCE per commit attempt (e.g. on the first
    ///     confirm/commit call for a given <c>TradeSession</c>) and MUST reuse the exact same value for every
    ///     retry of that same commit -- a fresh token per attempt defeats the dedupe entirely. Two genuinely
    ///     different trades (even between the same two characters) must use two different tokens.
    /// </param>
    /// <exception>
    ///     Throws SQL 50268 (Character A: unknown character or insufficient money balance) or 50269 (Character
    ///     B: same, for the other side) -- identical failure codes to
    ///     <see cref="ICharacterRepository.ExecuteTradeAsync" />, since a failed attempt never claims the ledger
    ///     row (the THROW rolls the whole transaction, including the ledger INSERT, back), so a legitimate retry
    ///     with the same token can still succeed later.
    /// </exception>
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
        long offeredMoneyB = 0, int offeredBigMoneyB = 0);
}
