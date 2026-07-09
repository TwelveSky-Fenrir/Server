using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 506, legacy's "moncall" command -- summon a monster at the invoking GM's own position
///     (Server/ts25zone/S04_MyWork04.cpp:1133-1145). There is no dedicated legacy wire opcode for this command
///     -- it rides the same generic envelope every other <see cref="GenericActionRequest" /> tSort uses, so this
///     is an embedded payload (<see cref="IFenrirWireType{TSelf}" />), never a standalone <c>[FenrirPacket]</c>.
///     <para>
///         The four-byte single-int shape this type decodes (Server/Header/Protocol/STRUCT.h:1270-1273) is, in
///         the legacy source, one physical struct definition aliased across four unrelated request-type names
///         covering four semantically unrelated sub-commands -- only one of which is tSort 506/"moncall". This
///         type is deliberately scoped to 506 alone and must not be reused as a generic "single-int GM payload"
///         for the other three unrelated sub-commands the legacy alias also covers; each of those, if/when
///         implemented, gets its own dedicated <see cref="IFenrirWireType{TSelf}" /> even though the wire shape
///         is byte-identical, so a future field addition to one never silently changes the others.
///     </para>
/// </summary>
[FenrirWireType(4)]
public readonly partial record struct GmSummonMonsterPayload : IFenrirWireType<GmSummonMonsterPayload>
{
    /// <summary>Monster template id to summon. Used directly with no validity check of any kind -- not checked
    /// positive, not checked to correspond to a real template.</summary>
    public required int Value { get; init; }
}
