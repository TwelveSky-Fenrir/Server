using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Admin-tier (<c>GmCommandTier.Admin</c>) "MAX" stat-cheat GM command: legacy
///     PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>) sub-command 509,
///     Server/ts25zone/S04_MyWork04.cpp:1188-1202 -- no wire payload is ever read for this selector, and no
///     response packet is ever composed for it on either the privilege-gate-failure or the success path
///     (both paths end in a forced full-logout disconnect instead) -- see this type's own implementation
///     remarks for the full contract.
/// </summary>
public interface IGmMaxStatService
{
    public ValueTask HandleAsync(ZoneClientSession zoneSession, PlayerRuntimeState state, Zone zone,
        CancellationToken cancellationToken);
}
