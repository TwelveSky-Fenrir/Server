using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Accounts;

// Interface (unlike AccountRepository) so GiftListHandler/ClaimGiftHandler can be unit-tested without a SQL container.
public interface IGiftRepository
{
    public ValueTask<ReadOnlyCollection<PendingGiftDto>> GetPendingByAccountAsync(int accountId, CancellationToken ct);

    /// <summary>
    ///     Atomically claims the gift into the shared vault. Throws SQL 50220 (not found/not owned/claimed) or 50274
    ///     (vault full, 28 slots).
    /// </summary>
    public ValueTask<short> ClaimIntoVaultAsync(int giftId, int accountId, CancellationToken ct);

    /// <summary>
    ///     Mints one pending gift for a single named account: one <c>game.Gifts</c> row (Status=Pending) plus a
    ///     matching <c>game.GiftLog</c> row, in one atomic call to <c>game.usp_Gift_Enqueue</c>. Returns the new
    ///     GiftId.
    /// </summary>
    /// <remarks>
    ///     This is new Fenrir capability, not a legacy port: the closest legacy analogue,
    ///     <c>MyDB::UpdateGift</c> (Server/ts25playuser/S08_MyDB.cpp:331-381), is dead code in every shipped
    ///     build -- its one call site is gated by a permanently commented-out
    ///     <c>//#define GIFT_ONLINE_EVENT</c> (Server/ts25playuser/S07_MyGame01.cpp:482-490), so there is no live
    ///     legacy behavior here to match byte-for-byte, only a designed-but-never-shipped shape to take as
    ///     inspiration. Accordingly:
    ///     <list type="bullet">
    ///         <item>
    ///             This method is single-recipient only. The legacy shape's "<paramref name="accountId" /> == 0
    ///             means broadcast to every account currently connected past the entered-zone milestone"
    ///             mode (Server/ts25playuser/S08_MyDB.cpp:363-374) is deliberately not implemented -- it is a
    ///             separate, undecided capability, not scoped to this method. Passing an account id that does not
    ///             correspond to a real row in <c>auth.Accounts</c> (including 0) fails the stored procedure's
    ///             foreign-key constraint rather than being interpreted as a broadcast.
    ///         </item>
    ///         <item>
    ///             <paramref name="productId" />, <paramref name="quantity" />, and <paramref name="value" /> are
    ///             passed through unvalidated -- no catalog check, no positivity/upper-bound check -- matching
    ///             both the dead legacy shape (no such check is observed anywhere in the cited range) and the
    ///             already-implemented `usp_Gift_Enqueue` contract itself
    ///             (Database/StoredProcedures/game/usp_Gift_Enqueue.sql). Whatever calls this (a cash-item
    ///             purchase reward, a future GM command, ...) owns validating those values against its own
    ///             business rule first; this method does not.
    ///         </item>
    ///     </list>
    ///     Which event actually calls this (which cash-item purchase, or a future GM command) is deliberately not
    ///     decided here -- that wiring belongs to whoever adds the trigger.
    /// </remarks>
    public ValueTask<int> EnqueueAsync(int accountId, int? productId, int quantity, int value, CancellationToken ct);
}
