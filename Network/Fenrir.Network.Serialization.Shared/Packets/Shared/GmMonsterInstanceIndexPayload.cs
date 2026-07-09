using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 508, the GM "Basic"-tier (legacy <c>uUserSort &gt;= 1</c>) "DIE" command -- force-invalidate a live
///     monster instance by raw table index (Server/ts25zone/S04_MyWork04.cpp:1165-1187). There is no dedicated
///     legacy wire opcode for this command -- it is multiplexed inside the same generic envelope every other
///     <see cref="GenericActionRequest" /> tSort uses, so this is an embedded payload
///     (<see cref="IFenrirWireType{TSelf}" />), never a standalone <c>[FenrirPacket]</c>.
///     <para>
///         The four-byte single-int shape (Server/Header/Protocol/STRUCT.h:1270-1273) is the same physical
///         legacy struct definition <see cref="GmCreateItemPayload" /> (tSort 505/523) and
///         <see cref="GmSummonMonsterPayload" /> (tSort 506) already model -- one C struct aliased across
///         several unrelated request-type names. This command is one of the "other unrelated sub-commands"
///         <see cref="GmSummonMonsterPayload" />'s own remarks flag as sharing that alias; per the same
///         convention already established there, DIE gets its own dedicated <see cref="IFenrirWireType{TSelf}" />
///         even though the wire shape is byte-identical to its siblings, so a future field addition to one
///         never silently changes the others.
///     </para>
///     <para>
///         Unlike its by-name-target siblings (FIND/CALL/MOVE/NCHAT/YCHAT/KICK), this command's target concept
///         has no relation to a player/avatar at all: MonsterIndex is a raw index into the world's live
///         monster-instance table (fixed 3000-entry capacity, Server/ts25zone/H01_MainApplication.h:76,97), not
///         an avatar name or index. Range validation (0 &lt;= index &lt; 3000, and "already invalid" rejection)
///         is the caller's responsibility, not this type's -- both cases are silently dropped upstream with no
///         mutation and no double-invalidation.
///     </para>
/// </summary>
[FenrirWireType(4)]
public readonly partial record struct GmMonsterInstanceIndexPayload : IFenrirWireType<GmMonsterInstanceIndexPayload>
{
    /// <summary>Index into the live monster-instance table. Valid range [0, 3000); an out-of-range or
    /// already-invalid index is silently dropped upstream with no mutation and no error signal distinct from
    /// "nothing happened". Validated by the caller, not here.</summary>
    public required int MonsterIndex { get; init; }
}
