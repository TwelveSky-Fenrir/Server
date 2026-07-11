using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 599, legacy's "GM-CALLPVP" command (Server/ts25zone/S04_MyWork04.cpp:1770-1823). There is no
///     dedicated legacy wire opcode for this command -- it is multiplexed inside the same generic envelope every
///     other <see cref="GenericActionRequest" /> tSort uses, so this is an embedded payload
///     (<see cref="IFenrirWireType{TSelf}" />), never a standalone <c>[FenrirPacket]</c>.
///     <para>
///         <b>Numbering note:</b> see <see cref="GmSetPvpPointPayload" />'s own remarks -- this tSort sits in the
///         same 598-603 numeric family <c>ContainerMatrix.KnownSorts</c> comments as "Scripted duel, map 124
///         only," but is confirmed unrelated to that subsystem (<c>Zone124MassDuelState</c> only consumes
///         601/602/603).
///     </para>
///     <para>
///         Field order (DuelSlot then TargetName, back-to-back, no other fields) mirrors
///         Server/Header/Protocol/STRUCT.h:1290-1294. TargetName's 13-byte fixed length matches the maximum
///         avatar-name length (Server/Header/Protocol/DEFINE.h:280), the same convention
///         <see cref="GmTargetNamePayload" /> already establishes for this dispatch table's other by-name
///         sub-commands -- but unlike every one of those siblings (FIND/CALL/MOVE/NCHAT/YCHAT/KICK, all of which
///         combine only a bare name with no leading field), this command's own STRUCT.h shape prepends DuelSlot
///         ahead of the name, so it is its own dedicated type rather than a reuse of
///         <see cref="GmTargetNamePayload" />.
///     </para>
///     <para>
///         Matching semantics (documented on the consuming service, not this wire type): the "A14-gm-remaining"
///         behavior contract states this command's name comparison is exact, case-sensitive, full-string
///         equality -- deliberately NOT the case-insensitive comparison
///         <c>ZoneRegistry.TryGetPlayerAndZoneByName</c>/<c>GmBasicCommandService</c>'s own by-name lookups use
///         for their sibling commands in this same dispatch table.
///     </para>
/// </summary>
[FenrirWireType(17)]
public readonly partial record struct GmCallPvpPayload : IFenrirWireType<GmCallPvpPayload>
{
    /// <summary>
    ///     Legal values 1 or 2 -- selects which of two fixed target coordinate triples a matched player is
    ///     relocated to. Any other value causes the command to be silently rejected (failure ack, no relocation
    ///     work attempted) upstream.
    /// </summary>
    public required int DuelSlot { get; init; }

    /// <summary>
    ///     The exact character name to search for among every character currently connected to this server
    ///     process. Matching is exact, case-sensitive, full-string equality -- not a prefix/substring match, and
    ///     not case-insensitive (see this type's own remarks).
    /// </summary>
    [FixedString(13)]
    public required string TargetName { get; init; }
}
