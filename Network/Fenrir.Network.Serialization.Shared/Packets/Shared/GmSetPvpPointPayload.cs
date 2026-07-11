using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 598, legacy's "GM-SETPVPPOINT" command (Server/ts25zone/S04_MyWork04.cpp:1755-1769). There is no
///     dedicated legacy wire opcode for this command -- it is multiplexed inside the same generic envelope every
///     other <see cref="GenericActionRequest" /> tSort uses, so this is an embedded payload
///     (<see cref="IFenrirWireType{TSelf}" />), never a standalone <c>[FenrirPacket]</c>.
///     <para>
///         <b>Numbering note:</b> <c>ContainerMatrix.KnownSorts</c> (Fenrir.Application.Game.Domain.Inventory)
///         groups tSort 598-603 together under one "Scripted duel, map 124 only" source comment, but only
///         600-603 are actually consumed by that subsystem
///         (<c>Fenrir.Application.Game.Domain.Combat.Zone124MassDuelState</c>'s own citation covers
///         sub-commands 601/602/603 only -- verified this session, no 598/599 reference exists there). tSort
///         598/599 are this unrelated, permission-gated GM command pair (uUserSort &gt;= 1, the same threshold
///         every other GM "Basic"-tier command in this dispatch table uses) per this type's own citations
///         (S04_MyWork04.cpp:1755-1823) -- not part of the scripted map-124 duel event. Flagged here since that
///         pre-existing comment could otherwise mislead a future reader into assuming 598/599 already have a
///         home in <c>Zone124MassDuelState</c>.
///     </para>
///     <para>
///         Field order (DuelSlot then PointValue, back-to-back, no other fields) mirrors
///         Server/Header/Protocol/STRUCT.h:1285-1289.
///     </para>
/// </summary>
[FenrirWireType(8)]
public readonly partial record struct GmSetPvpPointPayload : IFenrirWireType<GmSetPvpPointPayload>
{
    /// <summary>
    ///     Legal values 1 or 2 -- selects which of two fixed duel sides is being addressed. Any other value
    ///     causes the command to be silently rejected (failure ack, no mutation) upstream.
    /// </summary>
    public required int DuelSlot { get; init; }

    /// <summary>
    ///     Transmitted by the client but never read by any code path in the cited legacy source -- a confirmed
    ///     dead input (Server/ts25zone/S04_MyWork04.cpp:1755-1769). Carried here only because it occupies wire
    ///     space between DuelSlot and the end of this embedded struct; do not invent a meaning for it. See the
    ///     "A14-gm-remaining" behavior contract's own Edge cases / open question for the unrecoverable "what was
    ///     this ever meant to represent" question.
    /// </summary>
    public required int PointValue { get; init; }
}
