using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Admin-tier (<c>GmCommandTier.Admin</c>) unlabeled grant-pet-experience GM
///     command: legacy PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>) sub-command 700,
///     Server/ts25zone/S04_MyWork04.cpp:2062-2083 -- no dedicated legacy wire opcode, multiplexed inside the
///     shared envelope like every other tSort in this handler. Owns its own response composition (result code
///     is unconditionally the accepted value once the privilege gate passes -- see this type's own
///     implementation remarks) but, unlike <c>IGmCreateItemService</c>, always reaches the shared response
///     epilogue (never a silent-success path).
/// </summary>
public interface IGmPetExperienceGrantService
{
    ValueTask HandleAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}
