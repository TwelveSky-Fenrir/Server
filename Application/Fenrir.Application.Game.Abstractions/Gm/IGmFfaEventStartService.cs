using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Elevated-tier (<c>GmCommandTier.Elevated</c>) zone-wide FFA-start command: legacy
///     PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>) sub-command 333,
///     Server/ts25zone/S04_MyWork04.cpp:1097-1131 -- there is no dedicated legacy wire opcode for this action.
///     Reads the shared, cluster-wide FFA phase (<see cref="Domain.World.ZoneWar.ZoneCenterSiegeState.Zone335" />)
///     as its idle-state precondition and, only when idle, writes this GameServer process's own
///     <see cref="Domain.World.ZoneWar.Zone335StartTrigger" /> (countdown + start-trigger flag) -- the FFA-335
///     autonomous tick state machine that would actually consume those two process-local fields (legacy
///     <c>Process_Zone_335_FFA</c>) is a separate, currently unmodeled system; this type only reproduces the
///     two writes the GM command itself performs. Always reaches the shared response epilogue: an unauthorized
///     caller is disconnected outright with no reply (tier gate), otherwise the shared opcode-23 generic-action
///     ack (<c>GenericActionResponse</c>, Sort=333) reports success only when the idle precondition held.
/// </summary>
public interface IGmFfaEventStartService
{
    /// <summary><paramref name="data" /> is the raw, unmodified 130-byte tData blob to echo back verbatim.</summary>
    public ValueTask HandleAsync(GmFfaEventStartPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
