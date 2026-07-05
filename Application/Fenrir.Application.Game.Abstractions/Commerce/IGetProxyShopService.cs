using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>Business logic for CZ_GET_DEPUTY_PSHOP_SEND (opcode 108), extracted from <see cref="GetProxyShopHandler" />.</summary>
public interface IGetProxyShopService
{
    /// <summary>
    ///     <c>Sort</c> 1/2 resolve the CALLER's own shop regardless of ShopState (so a closed shop's owner can
    ///     still inspect/withdraw); <c>Sort</c> 3 resolves <c>AvatarName</c> and requires the target's shop to
    ///     be OPEN.
    /// </summary>
    public ValueTask<GetProxyShopResponse> GetAsync(GetProxyShopRequest packet, Zone zone, int characterId,
        CancellationToken cancellationToken);
}
