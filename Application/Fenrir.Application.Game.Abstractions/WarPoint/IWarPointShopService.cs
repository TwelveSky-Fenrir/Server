using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.WarPoint;

/// <summary>Outcome discriminator for a War-Point NPC-shop purchase attempt.</summary>
public enum WarPointBuyStatus
{
    /// <summary>
    ///     Not a War-Point transaction (NPC not War-Point, or item absent from the price table). The caller
    ///     falls through to the ordinary NPC-shop purchase path.
    /// </summary>
    NotHandled,

    /// <summary>A structural cheat (wrong-NPC request or destination-slot conflict) -- the caller aborts the session.</summary>
    Aborted,

    /// <summary>
    ///     A clean, well-formed rejection (insufficient War-Points or Contribution-Points) -- the caller
    ///     replies with a failure code; the session stays connected and no balance changed.
    /// </summary>
    SoftRejected,

    /// <summary>The purchase was applied -- the caller replies with a success code.</summary>
    Succeeded
}

/// <summary>Result of an <see cref="IWarPointShopService.TryBuyAsync" /> call.</summary>
public readonly record struct WarPointBuyServiceResult(WarPointBuyStatus Status)
{
    public static readonly WarPointBuyServiceResult NotHandled = new(WarPointBuyStatus.NotHandled);
    public static readonly WarPointBuyServiceResult Aborted = new(WarPointBuyStatus.Aborted);
    public static readonly WarPointBuyServiceResult SoftRejected = new(WarPointBuyStatus.SoftRejected);
    public static readonly WarPointBuyServiceResult Succeeded = new(WarPointBuyStatus.Succeeded);
}

/// <summary>
///     The War-Point (WP/CP dual-currency) branch of the NPC-shop-to-inventory purchase
///     (<c>USE_WAR_POINT_SYSTEM</c>, <c>Server/ts25zone/S04_MyWork05.cpp:1798-1988</c>). Invoked from the general
///     NPC-shop-buy action after its town-zone / NPC-proximity gate and NPC/item resolution have already run,
///     and BEFORE the ordinary shop-membership path: a War-Point item bypasses the ordinary
///     <c>iCheckNPCShop == 2</c> / rent-item gates, so it must be offered this service first.
///     <see cref="WarPointBuyStatus.NotHandled" /> means "not a War-Point transaction, use the ordinary path."
/// </summary>
public interface IWarPointShopService
{
    /// <param name="npcId">The transacting NPC index (already resolved from the request).</param>
    /// <param name="itemId">The requested item index (already resolved from the request).</param>
    /// <param name="requestedQuantity">Requested quantity -- meaningful only for a stackable item.</param>
    /// <param name="destinationPage">Destination inventory container byte (already validated in range).</param>
    /// <param name="destinationSlot">Destination slot index (already validated in range).</param>
    public ValueTask<WarPointBuyServiceResult> TryBuyAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, int npcId, int itemId, int requestedQuantity, byte destinationPage, byte destinationSlot,
        CancellationToken ct);
}
