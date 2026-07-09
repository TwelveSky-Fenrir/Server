using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob
///     for tSort 505, the base ("spawn-item") form of the GM item-creation command -- there is no dedicated
///     legacy wire opcode for this action, it is multiplexed inside the same generic envelope every other
///     tSort in this family uses (same posture as <see cref="GmBlockAvatarPayload" />/tSort 519). ItemId is the
///     only field the legacy base struct carries; no quantity field is ever read for this selector (that only
///     exists on the sibling tSort 523 form -- see <see cref="GmCreateItemQuantityPayload" />).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork04.cpp:1036-1095 (case 505/523 body: privilege gate, id-range/
///     catalog checks, quantity resolution) ; Server/Header/Protocol/STRUCT.h:1270-1273 (the base request
///     struct -- item id field only, shared with several unrelated sub-commands via the same typedef) ;
///     Server/Header/Protocol/DEFINE.h:377-381 (fixed 100-byte tData payload; the alternate 130-byte branch
///     this project already models via <see cref="GenericActionRequest" />.Data is unreachable dead code in
///     every shipped build) ; Server/ts25zone/S05_MyTransfer.cpp:544-559 (shared response echoes the raw
///     request payload byte-for-byte unmodified, so this type is never used to build the response, only to
///     decode the request).
/// </remarks>
[FenrirWireType(4)]
public readonly record struct GmCreateItemPayload : IFenrirWireType<GmCreateItemPayload>
{
    /// <summary>Catalog item id to create. Valid range [2, 99999]; validated by the caller, not here.</summary>
    public required int ItemId { get; init; }
}
