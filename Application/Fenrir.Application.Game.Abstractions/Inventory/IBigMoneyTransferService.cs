using Fenrir.Application.Game.Abstractions.GenericAction;

namespace Fenrir.Application.Game.Abstractions.Inventory;

/// <summary>
///     Business logic behind <c>GenericActionHandler</c>'s CZ_PROCESS_DATA_SEND BigMoney ("1B"/"1 Md")
///     Store/Save transfer dispatch: tSort 241/244 (Inventory &lt;-&gt; Store) and 242/245 (Inventory &lt;-&gt;
///     Save/vault). Neither touches <c>Zone</c>/<c>PlayerRuntimeState</c> at all -- same posture as
///     <c>IGenericActionService.TransferBankMoneyAsync</c> (231/232), the ordinary-money sibling of this same
///     shape: <c>PlayerRuntimeState</c> deliberately never caches any BigMoney counter (matching its own
///     "never caches wallet Money" precedent), and the wire reply for this whole family only ever echoes the
///     request bytes + a result code, never a fresh balance (C8 contract's own Outputs section) -- so there is
///     nothing for a zone-tick mirror to do.
/// </summary>
/// <remarks>
///     Réf. contract: C8-hotkey-money-petbag (legacy-behavior-translator, wave7). Only the request-shape check
///     (amount must be at least 1) is evaluated directly by this service -- the source-balance/destination-cap
///     checks are enforced entirely by the underlying <c>IBigMoneyRepository</c> procedure's own guarded
///     UPDATE, matching <c>IGenericActionService.TransferStoreMoneyAsync</c>'s own established precedent for
///     the identical reason (no cached balance to check against here either).
///     <para>
///         tSort 240/243 (Inventory &lt;-&gt; Trade-offer BigMoney) are deliberately NOT covered by this
///         service -- see the C8 wiring manifest's open questions for why (the broader trade-money-placement
///         subsystem, <c>TradeSession</c>/<c>TradeMoneyPlacementResolver</c>/<c>TradeBigMoneyPlacementResolver</c>,
///         has zero live callers anywhere in this codebase yet and needs its own dedicated wiring pass, not a
///         half-built piece bolted onto this one).
///     </para>
/// </remarks>
public interface IBigMoneyTransferService
{
    /// <summary>
    ///     tSort 241 (Inventory BigMoney -&gt; Store BigMoney) / 244 (the reverse). <paramref name="data" /> is
    ///     the raw <see cref="Fenrir.Network.Serialization.Shared.Packets.Shared.DefaultPData" /> payload;
    ///     only its Quantity1 field is consulted, matching the C8 contract's own Inputs section.
    /// </summary>
    public ValueTask<GenericActionResult> TransferStoreAsync(int sort, byte[] data, int characterId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     tSort 242 (Inventory BigMoney -&gt; Save/vault BigMoney) / 245 (the reverse).
    /// </summary>
    public ValueTask<GenericActionResult> TransferSaveAsync(int sort, byte[] data, int accountId, int characterId,
        CancellationToken cancellationToken);
}
