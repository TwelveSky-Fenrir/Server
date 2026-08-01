using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public enum OpenShopStallPrepareOutcome
{
    Abort,

    LiveOpened,

    ProxyReady,

    Blocked
}

public readonly record struct OpenShopStallPrepareResult(
    OpenShopStallPrepareOutcome Outcome,
    OpenShopStallResponse? Response,
    PshopInfo Listing,
    List<OfflineShopItemSlotTvp>? OfflineItems);

public interface IOpenShopStallService
{
    public ValueTask<OpenShopStallPrepareResult> PrepareAsync(OpenShopStallRequest packet, PlayerRuntimeState state,
        CancellationToken cancellationToken);

    public ValueTask<OpenShopStallResponse> OpenProxyShopAsync(OpenShopStallRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, PshopInfo listing,
        List<OfflineShopItemSlotTvp> offlineItems, CancellationToken cancellationToken);
}
