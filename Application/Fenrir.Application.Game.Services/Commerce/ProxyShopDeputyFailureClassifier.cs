using Microsoft.Data.SqlClient;

namespace Fenrir.Application.Game.Services.Commerce;

/// <summary>
///     C21a fix for the "eight legacy failure codes ... intentionally left collapsed to 1 / 2" gap
///     <see cref="UpdateProxyShopService" />'s own class remarks already flagged. The C21 contract confirms
///     two of <c>CZ_SET_DEPUTY_PSHOP_SEND</c>'s (opcode 109) ten legacy outcomes are NOT coded wire replies
///     at all -- they hard-disconnect the session instead:
///     <list type="number">
///         <item>
///             an invalid requested action code (neither "retrieve" nor "purchase," including the
///             three-member enum's own "none" value) -- already handled correctly by
///             <see cref="UpdateProxyShopService.Validate" />'s <c>BuySort</c> range check
///             (<c>UpdateProxyShopHandler</c> aborts on <c>validation.Abort</c>); the contract only confirms
///             this existing behavior, no code change needed for it.
///         </item>
///         <item>
///             a stale inventory page detected specifically on the "purchase" path (<c>BuySort</c> = 2) --
///             NOT previously modeled: <see cref="UpdateProxyShopService.PurchaseAsync" />'s generic
///             <c>catch (Exception ex)</c> around <c>ExecutePurchaseAsync</c> currently answers EVERY
///             failure, stale-listing included, with a coded Result=2 reply. This class exists to let that
///             catch block distinguish the stale-listing case and hard-disconnect instead, per the contract
///             -- see the wiring manifest for the exact catch-clause split this class supports.
///         </item>
///     </list>
///     <see cref="StaleListingSqlErrorNumber" /> reuses the exact SQL error number
///     <c>BuyShopItemService.ProxyListingStaleErrorNumber</c> already established for the SAME stored
///     procedure (<c>usp_OfflineShop_ExecutePurchase.sql</c>, <c>THROW 50272</c>) -- both
///     <see cref="UpdateProxyShopService.PurchaseAsync" /> (opcode 109, <c>BuySort</c>=2) and
///     <c>BuyShopItemService.CommitProxyPurchaseAsync</c> (opcode 35) call the identical
///     <c>IOfflineShopRepository.ExecutePurchaseAsync</c>, so the same stale-listing SQL error can surface on
///     either call site. Opcode 35's own handling of this error is a coded Result=4 reply; the C21 contract
///     confirms opcode 109's handling of the SAME underlying condition is a hard disconnect instead -- the
///     two opcodes deliberately diverge here, per the contract, and this is not an inconsistency to
///     reconcile.
/// </summary>
/// <remarks>
///     Réf. contract : C21-commerce-social.md §C (Trigger/Outputs/Edge cases/Citations C), sourced from
///     <c>Server/ts25zone/S07_MyGame09.cpp:594-599,602-644,641-643,681-683</c>.
///     <para>
///         The full itemized 7-failure-code-to-condition mapping for opcode 109 (beyond this one recoverable
///         fact) remains an explicit open question -- the contract itself states no numeric-value-to-condition
///         mapping is fully recoverable from the cited lines beyond "code 2 is reused for three distinct
///         retrieve-path meanings," which does not by itself resolve which of the seven failure codes each of
///         <see cref="UpdateProxyShopService" />'s own two remaining generic catch-alls (Result=1 for
///         retrieve, Result=2 for purchase minus the one case this class now splits out) should really send.
///         <see cref="UpdateProxyShopService" />'s own class remarks are intentionally left otherwise
///         unchanged pending a dedicated legacy-behavior-translator follow-up contract supplying the full
///         ten-code enumeration and the exact SQL error each stored procedure throws per case -- see this
///         change's own <c>openQuestions</c> for the explicit pointer.
///     </para>
/// </remarks>
public static class ProxyShopDeputyFailureClassifier
{
    /// <summary>
    ///     SQL error THROWn by <c>usp_OfflineShop_ExecutePurchase.sql</c> when the listing slot no longer
    ///     matches what the caller resolved a moment earlier (another buyer already claimed it, or the shop
    ///     closed in the interim) -- the same SQL error number as
    ///     <c>BuyShopItemService.ProxyListingStaleErrorNumber</c>
    ///     (<c>Database/StoredProcedures/game/usp_OfflineShop_ExecutePurchase.sql:42-43</c>, SQL 50272),
    ///     reused here because both call sites invoke the identical stored procedure.
    /// </summary>
    public const int StaleListingSqlErrorNumber = 50272;

    /// <summary>
    ///     True when <paramref name="ex" /> is the stale-proxy-listing failure that must hard-disconnect the
    ///     session on the <c>CZ_SET_DEPUTY_PSHOP_SEND</c> purchase path (<c>BuySort</c>=2), per the C21
    ///     contract's Edge cases C -- rather than falling into the generic catch-all that, before this fix,
    ///     returned a coded Result=2 reply for every <c>ExecutePurchaseAsync</c> failure indiscriminately.
    /// </summary>
    public static bool IsStaleListingFailure(Exception ex)
    {
        return ex is SqlException { Number: StaleListingSqlErrorNumber };
    }
}
