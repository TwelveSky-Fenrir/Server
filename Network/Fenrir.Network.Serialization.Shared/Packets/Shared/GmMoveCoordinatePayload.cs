using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob
///     for tSort 507, the GM "Basic"-tier (legacy <c>uUserSort &gt;= 1</c>) self-teleport-to-coordinate command
///     (Server/ts25zone/S04_MyWork04.cpp:1146-1164). There is no dedicated legacy wire opcode for this
///     command -- it is multiplexed inside the same generic envelope every other tSort in this family uses
///     (see <see cref="GmTargetNamePayload" />'s own remarks for the sibling name-only sub-commands, and
///     <c>GmBlockAvatarPayload</c> for tSort 519's identical transport shape), so this is an embedded payload
///     (<see cref="IFenrirWireType{TSelf}" />), never a standalone <c>[FenrirPacket]</c>.
///     <para>
///         Location is the only field this sub-command reads: an absolute world position (x, y, z),
///         unconditionally applied to the invoking GM's own current position with no bounds, map-validity,
///         or collision check (Server/Header/Protocol/STRUCT.h:1274-1277 -- coordinate field shape). A
///         secondary per-session permission flag is read on entry in the cited lines but its enforcement
///         branch is entirely commented out in the source (dead code at S04_MyWork04.cpp:1154-1159) -- not
///         reproduced here, since it never actually gated anything in the legacy build this contract mirrors.
///     </para>
///     <para>
///         Field order/shape mirrors <see cref="Shared.ActionInfo" />'s own established "3 consecutive
///         little-endian floats = a position triple" convention (its <c>Location</c> field), rather than
///         inventing a new representation for the same concept.
///     </para>
/// </summary>
[FenrirWireType(12)]
public readonly partial record struct GmMoveCoordinatePayload : IFenrirWireType<GmMoveCoordinatePayload>
{
    [FixedArray(3)] public required float[] Location { get; init; }
}
