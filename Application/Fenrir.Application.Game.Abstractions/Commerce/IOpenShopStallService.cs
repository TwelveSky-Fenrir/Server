using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>The three ways <see cref="OpenShopStallService.Prepare" /> can leave <see cref="OpenShopStallHandler" />.</summary>
public enum OpenShopStallPrepareOutcome
{
    /// <summary>A protocol-level fault -- the handler should abort the session.</summary>
    Abort,

    /// <summary>
    ///     The live personal shop was opened synchronously; the handler should send
    ///     <see cref="OpenShopStallPrepareResult.LiveResponse" />.
    /// </summary>
    LiveOpened,

    /// <summary>
    ///     The offline/deputy shop's slots validated; the handler should acquire the economy lock and call
    ///     <see cref="IOpenShopStallService.OpenProxyShopAsync" />.
    /// </summary>
    ProxyReady
}

public readonly record struct OpenShopStallPrepareResult(
    OpenShopStallPrepareOutcome Outcome,
    OpenShopStallResponse? LiveResponse,
    PshopInfo Listing,
    List<OfflineShopItemSlotTvp>? OfflineItems);

/// <summary>Business logic for CZ_START_PSHOP_SEND (opcode 31), extracted from <see cref="OpenShopStallHandler" />.</summary>
public interface IOpenShopStallService
{
    /// <summary>
    ///     Validates the submitted stall and either opens the LIVE shop synchronously (a pure display overlay --
    ///     items never leave inventory) or, for the offline/deputy shop, validates every occupied slot against
    ///     the live inventory and hands back the data <see cref="OpenProxyShopAsync" /> needs.
    /// </summary>
    public OpenShopStallPrepareResult Prepare(OpenShopStallRequest packet, PlayerRuntimeState state);

    /// <summary>
    ///     Physically removes the advertised items from inventory into the offline shop and persists it. The
    ///     caller's <see cref="PlayerRuntimeState.EconomyActionLock" /> must already be held.
    /// </summary>
    public ValueTask<OpenShopStallResponse> OpenProxyShopAsync(OpenShopStallRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, PshopInfo listing, List<OfflineShopItemSlotTvp> offlineItems,
        CancellationToken cancellationToken);
}
