using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Elevated-tier (<c>GmCommandTier.Elevated</c>) "[GM]-EXP" (grant experience to
///     self) command: legacy PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>) sub-command 503,
///     Server/ts25zone/S04_MyWork04.cpp:959-1005 -- there is no dedicated legacy wire opcode for this action;
///     <c>GenericActionHandler</c>'s own tSort 503 branch decodes <see cref="GmExpGrantPayload" /> out of the
///     shared envelope's tData blob before invoking this. Target is always the invoking GM's own character/pet
///     -- there is no avatar-name or target field in the request. Always reaches the shared response epilogue:
///     an unauthorized caller is disconnected outright with no reply (tier gate), every other outcome --
///     including every documented no-op branch (already-at-cap, unrecognized mode) -- reports the shared
///     opcode-23 generic-action ack (<c>GenericActionResponse</c>, Sort=503) with an unconditional success
///     result code.
/// </summary>
public interface IGmExpGrantService
{
    /// <summary><paramref name="data" /> is the raw, unmodified 130-byte tData blob to echo back verbatim.</summary>
    public ValueTask HandleAsync(GmExpGrantPayload packet, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);
}
