namespace Fenrir.Application.Game.Domain.Social.Trade;

/// <summary>
///     Idempotency-token factory for a two-character trade commit (C8-trade-finalize). The commit stored
///     procedure's own doc comment names this exact factory as the caller-side convention
///     (Database/StoredProcedures/game/usp_CharacterTradeCommit_ExecuteIdempotent.sql:13): generate ONCE per
///     commit attempt and reuse the SAME value for every retry of that same attempt -- a fresh token per retry
///     defeats <c>game.TradeCommitLedger</c>'s dedupe entirely (see that table's own remarks). A thin, named
///     wrapper over <see cref="Guid.NewGuid" /> rather than a bare call at the use site, so the
///     "one token per commit attempt, never regenerated mid-retry" rule has a single, greppable home instead of
///     being documented only in a SQL comment.
/// </summary>
public static class TradeCommitToken
{
    /// <summary>
    ///     A fresh idempotency token for a brand-new commit attempt. Never call this again to retry an attempt
    ///     that already generated one -- capture the returned value and reuse it verbatim for that retry.
    /// </summary>
    public static Guid NewForCommit()
    {
        return Guid.NewGuid();
    }
}
