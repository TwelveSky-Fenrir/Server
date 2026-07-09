using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Elevated-tier (<c>GmCommandTier.Elevated</c>) "moncall" (summon monster) command:
///     legacy PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>) sub-command 506,
///     Server/ts25zone/S04_MyWork04.cpp:1133-1145 -- there is no dedicated legacy wire opcode for this action;
///     <c>GenericActionHandler</c>'s own tSort 506 branch decodes <see cref="GmSummonMonsterPayload" /> out of
///     the shared envelope's tData blob before invoking this. Spawns at the invoking GM's own current position
///     -- there is no location/target field in the request. Performs no validation of the requested monster
///     template id (matching legacy's own complete absence of a check here, unlike the separately-modeled
///     <c>boss</c> chat command's own <c>&lt; 1</c> rejection -- see this type's own implementation remarks for
///     the "known functional overlap, not resolved" posture the source behavior contract requires). Always
///     reaches the shared response epilogue: an unauthorized caller is disconnected outright with no reply
///     (tier gate), every other outcome -- including a nonexistent/invalid template id -- reports the shared
///     opcode-23 generic-action ack (<c>GenericActionResponse</c>, Sort=506) with an unconditional success
///     result code.
/// </summary>
public interface IGmSummonMonsterService
{
    /// <summary><paramref name="data" /> is the raw, unmodified 130-byte tData blob to echo back verbatim.</summary>
    public ValueTask HandleAsync(GmSummonMonsterPayload packet, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);
}
