using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob,
///     shared by six same-family, same-envelope GM "Basic"-tier (legacy <c>uUserSort &gt;= 1</c>)
///     by-name-target sub-commands whose request payload is nothing but a target character name:
///     <list type="bullet">
///         <item>tSort 513 FIND (Server/ts25zone/S04_MyWork04.cpp:1299-1323)</item>
///         <item>tSort 514 CALL (Server/ts25zone/S04_MyWork04.cpp:1324-1384)</item>
///         <item>tSort 515 MOVE-to-target's-position (Server/ts25zone/S04_MyWork04.cpp:1385-1410)</item>
///         <item>tSort 516 NCHAT (Server/ts25zone/S04_MyWork04.cpp:1411-1439)</item>
///         <item>tSort 517 YCHAT (Server/ts25zone/S04_MyWork04.cpp:1440-1468)</item>
///         <item>tSort 518 KICK (Server/ts25zone/S04_MyWork04.cpp:1469-1486)</item>
///     </list>
///     There is no dedicated legacy wire opcode for any of these -- each is multiplexed inside the same
///     generic envelope every other tSort in this family uses (mirrors <c>GmBlockAvatarPayload</c>'s own
///     tSort 519 shape, and see <see cref="GmMoveCoordinatePayload" /> for tSort 507's sibling coordinate
///     payload), so this is an embedded payload (<see cref="IFenrirWireType{TSelf}" />), never a standalone
///     <c>[FenrirPacket]</c>.
///     <para>
///         <b>Citation precision note:</b> the behavior contract for tSort 515 (MOVE-to-target) explicitly
///         cites <c>Server/Header/Protocol/STRUCT.h:1278-1281</c> as a struct shared with CALL (514) and KICK
///         (518) -- those three are confirmed to read the identical legacy C struct. FIND (513), NCHAT (516),
///         and YCHAT (517) each describe an identically-shaped field ("target character name, up to 13
///         characters including terminator," <c>Server/Header/Protocol/DEFINE.h:280</c>) in their own
///         contracts, but no citation confirms they read that same named STRUCT.h struct rather than three
///         independent (byte-identical) declarations. Reusing one C# type across all six here is therefore a
///         Fenrir-side transport-layer convenience -- one wire shape, one type -- not an assertion that all
///         six share one legacy struct; flag for <c>cpp-protocol-header-analyst</c> re-check if a future
///         change needs that confirmed rather than inferred for FIND/NCHAT/YCHAT specifically.
///     </para>
///     <para>
///         Per-command defensive-re-termination note (decode-time validation, not a wire-shape difference):
///         FIND's own contract states its name field is "defensively re-terminated/bounds-checked before
///         use" ahead of the search call, while CALL/MOVE(515)/KICK/NCHAT/YCHAT's own contracts each flag the
///         <i>absence</i> of that same defensive pass as an observed (not judged) asymmetry. This type only
///         models the wire shape (13-byte fixed string); any defensive re-termination is a handler-side
///         concern for whichever command needs it, not something this payload type performs itself.
///     </para>
/// </summary>
[FenrirWireType(13)]
public readonly partial record struct GmTargetNamePayload : IFenrirWireType<GmTargetNamePayload>
{
    [FixedString(13)] public required string TargetName { get; init; }
}
