using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob for
///     tSort 701, legacy's "GM_CLEAR_INVENTORY" command (Server/ts25zone/S04_MyWork04.cpp:2084-2111). There is
///     no dedicated legacy wire opcode for this command -- it is multiplexed inside the same generic envelope
///     every other <see cref="GenericActionRequest" /> tSort uses, so this is an embedded payload
///     (<see cref="IFenrirWireType{TSelf}" />), never a standalone <c>[FenrirPacket]</c>. Operates exclusively
///     on the invoking GM's own inventory -- there is no target-character field anywhere in this payload.
///     <para>
///         Single-field layout (PageSelector at offset 0, no other fields) mirrors
///         Server/Header/Protocol/STRUCT.h:1302-1304.
///     </para>
/// </summary>
[FenrirWireType(4)]
public readonly partial record struct GmClearInventoryPayload : IFenrirWireType<GmClearInventoryPayload>
{
    /// <summary>
    ///     Which inventory page of the invoking GM's own character is to be cleared: 0 = first page only, 1 =
    ///     second page only. Any other value (negative, or 2 and above) is not rejected -- it is instead treated
    ///     as a request to clear both pages (see the "A14-gm-remaining" behavior contract's own Edge cases).
    /// </summary>
    public required int PageSelector { get; init; }
}
