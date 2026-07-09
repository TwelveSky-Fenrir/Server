using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 519, legacy's "[GM]-BLOCK" command (Server/ts25zone/S04_MyWork04.cpp:1487-1515). There is no
///     dedicated legacy wire opcode for GM-BLOCK -- it is multiplexed inside the same generic envelope every
///     other <see cref="GenericActionRequest" /> tSort uses, so this is an embedded payload
///     (<see cref="IFenrirWireType{TSelf}" />), never a standalone <c>[FenrirPacket]</c>. AvatarName is the only
///     field this sub-command reads: the (sanitized) name of the online avatar to permanently block.
///     <para>
///         Field shape (offset 0, 13-byte fixed string, no other fields) carries over unchanged from the
///         previous (misrouted) standalone <c>[FenrirPacket]</c> declaration this type replaces -- that
///         13-byte-name assumption itself was not re-derived here, only its transport was fixed (first 13 bytes
///         of the 130-byte tData blob, instead of a whole synthetic packet body on a wire opcode no real client
///         ever sends). It also matches <see cref="GuildWorkKickPayload" />'s already-shipped "name at offset 0,
///         13 bytes" shape for CZ_GUILD_WORK_SEND's own by-name-target sub-command, a different tSort family
///         riding inside its own unrelated generic envelope. Neither of those is a byte-for-byte citation of
///         S04_MyWork04.cpp:1487-1515's own field reads for tSort 519 specifically -- flag for
///         <c>cpp-zone-gameplay-analyst</c> re-check if a future change needs that confirmed field-exact rather
///         than inferred from the pre-existing accepted decode and the sibling by-name-target convention.
///     </para>
/// </summary>
[FenrirWireType(13)]
public readonly record struct GmBlockAvatarPayload : IFenrirWireType<GmBlockAvatarPayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
