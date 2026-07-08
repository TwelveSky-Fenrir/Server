using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>The four ways <see cref="OpenShopStallService.PrepareAsync" /> can leave <see cref="OpenShopStallHandler" />.</summary>
public enum OpenShopStallPrepareOutcome
{
    /// <summary>A protocol-level fault -- the handler should abort the session.</summary>
    Abort,

    /// <summary>
    ///     The live personal shop was opened synchronously; the handler should send
    ///     <see cref="OpenShopStallPrepareResult.Response" />.
    /// </summary>
    LiveOpened,

    /// <summary>
    ///     The offline/deputy shop's slots validated; the handler should acquire the economy lock and call
    ///     <see cref="IOpenShopStallService.OpenProxyShopAsync" />.
    /// </summary>
    ProxyReady,

    /// <summary>
    ///     The cross-type "shop already open" exclusivity gate rejected the request with a coded, non-disconnecting
    ///     response (result 101/102/103) -- the handler should send <see cref="OpenShopStallPrepareResult.Response" />
    ///     without aborting the session, mirroring legacy's non-disconnecting coded responses for these cases
    ///     (Server/ts25zone/S04_MyWork02.cpp:6067-6095).
    /// </summary>
    Blocked
}

public readonly record struct OpenShopStallPrepareResult(
    OpenShopStallPrepareOutcome Outcome,
    OpenShopStallResponse? Response,
    PshopInfo Listing,
    List<OfflineShopItemSlotTvp>? OfflineItems);

/// <summary>Business logic for CZ_START_PSHOP_SEND (opcode 31), extracted from <see cref="OpenShopStallHandler" />.</summary>
public interface IOpenShopStallService
{
    /// <summary>
    ///     Validates the submitted stall and either opens the LIVE shop synchronously (a pure display overlay --
    ///     items never leave inventory) or, for the offline/deputy shop, validates every occupied slot against
    ///     the live inventory and hands back the data <see cref="OpenProxyShopAsync" /> needs. Also enforces the
    ///     cross-type "shop already open" exclusivity gate (personal-vs-proxy): a PROXY request while a PERSONAL
    ///     shop is open is rejected synchronously (101), and a PERSONAL request round-trips the offline-shop
    ///     repository to check for a confirmed-active proxy shop (102) or a failed round trip (103) -- see
    ///     <see cref="OpenShopStallPrepareOutcome.Blocked" />.
    /// </summary>
    public ValueTask<OpenShopStallPrepareResult> PrepareAsync(OpenShopStallRequest packet, PlayerRuntimeState state,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Physically removes the advertised items from inventory into the offline shop and persists it. The
    ///     caller's <see cref="PlayerRuntimeState.EconomyActionLock" /> must already be held.
    /// </summary>
    /// <param name="accountId">
    ///     The acting player's account id -- carried only for the game.EventLog audit row written per
    ///     accepted slot once persistence succeeds (legacy <c>GL_1000_PXSHOP_REG</c>); not used for any
    ///     validation or persistence decision.
    /// </param>
    public ValueTask<OpenShopStallResponse> OpenProxyShopAsync(OpenShopStallRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, PshopInfo listing,
        List<OfflineShopItemSlotTvp> offlineItems, CancellationToken cancellationToken);
}
