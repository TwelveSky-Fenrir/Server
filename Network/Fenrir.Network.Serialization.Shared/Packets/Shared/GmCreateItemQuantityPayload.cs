using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

/// <summary>
///     Re-read layer over CZ_PROCESS_DATA_SEND's (opcode 19, <see cref="GenericActionRequest" />) tData blob
///     for tSort 523, the explicit-quantity form of the GM item-creation command -- same "no dedicated wire
///     opcode, multiplexed inside the shared envelope" posture as <see cref="GmCreateItemPayload" />
///     (tSort 505). Field order is ItemId then Quantity, extending the base struct by exactly one trailing
///     field.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork04.cpp:1036-1095 (case 505/523 body) ;
///     Server/ts25zone/S04_MyWork04.cpp:1056-1060 (this quantity-variant struct -- declared locally inside the
///     handler itself, not present in any shared protocol header, unlike the base struct at STRUCT.h:1270-1273) ;
///     Server/Header/Protocol/DEFINE.h:611 (MAX_ITEM_DUPLICATION_NUM = 999, the stackable-item duplication
///     cap a quantity here is validated against by the caller, not this type).
/// </remarks>
[FenrirWireType(8)]
public readonly partial record struct GmCreateItemQuantityPayload : IFenrirWireType<GmCreateItemQuantityPayload>
{
    /// <summary>Catalog item id to create. Valid range [2, 99999]; validated by the caller, not here.</summary>
    public required int ItemId { get; init; }

    /// <summary>
    ///     Requested duplicate count. 0 means "unspecified" (resolved by the caller per the item's
    ///     stackable/unique classification); validated by the caller, not here.
    /// </summary>
    public required int Quantity { get; init; }
}
