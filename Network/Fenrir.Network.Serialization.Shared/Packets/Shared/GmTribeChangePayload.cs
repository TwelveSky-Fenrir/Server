using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 510, the GM "Basic"-tier (legacy <c>uUserSort &gt;= 1</c>) self tribe-change command
///     (Server/ts25zone/S04_MyWork04.cpp:1203-1264). There is no dedicated legacy wire opcode for this command
///     -- it is multiplexed inside the same generic envelope every other <see cref="GenericActionRequest" />
///     tSort uses, so this is an embedded payload (<see cref="IFenrirWireType{TSelf}" />), never a standalone
///     <c>[FenrirPacket]</c>. No target-character parameter exists in this payload at all -- this command can
///     only ever change the invoking GM's own tribe; a "force-set a target's tribe" action, if wanted, is a
///     different (unimplemented) command, not this one.
///     <para>
///         Single-field layout (Tribe at offset 0, no other fields) is inferred from the cited case body's own
///         read order: the tier gate runs, an inert per-account flag is checked (dead branch, never enforced,
///         not reproduced by this type), then exactly one selector value is read and validated against the live
///         0-3 range before any mutation (S04_MyWork04.cpp:1239-1262). An older, alternate implementation of the
///         selector logic sits commented out at S04_MyWork04.cpp:1214-1237 and does not execute -- not a source
///         for this shape. The behavior contract backing this struct does not cite a
///         <c>Server/Header/Protocol/STRUCT.h</c> location for this specific inline request shape -- same
///         "inferred from the accepted decode order, not independently byte-offset-confirmed" posture
///         <see cref="GmFfaEventStartPayload" />'s own remarks already document for its own inline tSort request
///         shape -- flag for <c>cpp-protocol-header-analyst</c> re-check if a future change needs field-exact
///         confirmation.
///     </para>
/// </summary>
[FenrirWireType(4)]
public readonly partial record struct GmTribeChangePayload : IFenrirWireType<GmTribeChangePayload>
{
    /// <summary>Tribe selector. Valid range [0, 3] -- 3 is a distinct special case (only the current tribe is
    /// set to 3; the previous-tribe marker is left untouched). A value equal to the character's current tribe,
    /// or outside [0, 3], is silently dropped with no mutation, no disconnect, and no acknowledgment of any
    /// kind. Validated by the caller, not here.</summary>
    public required int Tribe { get; init; }
}
