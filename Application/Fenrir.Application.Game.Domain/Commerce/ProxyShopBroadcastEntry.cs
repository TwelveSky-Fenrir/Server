namespace Fenrir.Application.Game.Domain.Commerce;

/// <summary>
///     One open offline/deputy ("proxy") shop as tracked by its owning <see cref="World.Zone" /> purely for the
///     periodic radius rebroadcast (<c>Zone.RebroadcastProxyShops</c>) -- independent of
///     <see cref="World.PlayerRuntimeState" />/<c>Zone._players</c> since the shop's whole point is that it keeps
///     advertising after its owner disconnects. Registered by <c>OpenShopStallService.OpenProxyShopAsync</c>,
///     removed by an explicit close (<c>CloseShopStallService.CloseOfflineShopAsync</c>) or by the zone's own
///     expiry check.
/// </summary>
/// <remarks>
///     <see cref="CharacterId" /> doubles as this shop's wire <c>ServerIndex</c> -- the legacy walks a fixed
///     shard-local slot table (Server/ts25zone/S07_MyGame01.cpp:2600-2607) that Fenrir has no equivalent
///     in-memory structure for; CharacterId is already a stable, unique per-shop key (one shop per character,
///     <c>game.OfflineShops</c>'s own primary key), so reusing it directly avoids minting a second, purely
///     synthetic id space for the same object -- the same choice already made for avatar broadcast ServerIndex
///     (<c>PlayerRuntimeState.CharacterId</c>).
/// </remarks>
public sealed class ProxyShopBroadcastEntry(
    int characterId,
    int uniqueNumber,
    string ownerName,
    string shopName,
    float posX,
    float posY,
    float posZ,
    int shopDate)
{
    public int CharacterId { get; } = characterId;

    /// <summary>
    ///     The wire <c>UniqueNumber</c> -- <c>characterId * 2 + 1</c>, the same proxy-shop-identifier formula
    ///     <c>OpenShopStallService.Prepare</c> already stamps onto <c>PshopInfo.UniqueNumber</c> for this same
    ///     shop, kept consistent here rather than minted independently.
    /// </summary>
    public int UniqueNumber { get; } = uniqueNumber;

    /// <summary>The shop owner's character/avatar name -- wire <c>ProxyStateInfo.Name</c>.</summary>
    public string OwnerName { get; } = ownerName;

    /// <summary>The shop's own display name -- wire <c>ProxyStateInfo.PshopName</c>.</summary>
    public string ShopName { get; } = shopName;

    public float PosX { get; } = posX;
    public float PosY { get; } = posY;
    public float PosZ { get; } = posZ;

    /// <summary>
    ///     YYYYMMDD expiration (<see cref="Simulation.GameDate" />); force-closed once today passes this.
    ///     Settable (not just init) so a rental-extension consumable's use
    ///     (<c>UseInventoryItemService</c>'s proxy-shop-rental-extension branch, via
    ///     <c>Zone.TryUpdateProxyShopExpiration</c>) can advance an already-registered shop's expiration in
    ///     place without discarding/re-registering the whole broadcast entry.
    /// </summary>
    public int ShopDate { get; set; } = shopDate;

    /// <summary>
    ///     Zone-clock timestamp of this shop's own last periodic broadcast (or force-close) consideration --
    ///     mutated only from the owning zone's tick thread, matching <c>MonsterEntity.LastRebroadcastAt</c>'s
    ///     posture.
    /// </summary>
    public TimeSpan LastBroadcastAt { get; set; }
}
