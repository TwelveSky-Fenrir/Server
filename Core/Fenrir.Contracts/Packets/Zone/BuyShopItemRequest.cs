using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_BUY_PSHOP_SEND (CLIENT.h:325-336) — buy from an open shop stall. <c>Page1</c>/<c>Index1</c> is
///     the seller's stall slot; <c>Page2</c>/<c>Index2</c>/<c>XPost2</c>/<c>YPost2</c> is the buyer's
///     destination inventory slot. No <c>PPSHOP_V2</c> guard here (asymmetric with 31-34): uses
///     <c>IsValidTownAll</c> instead.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.BuyShopItem, ExpectedSize = 54,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct BuyShopItemRequest : IIncomingPacket<BuyShopItemRequest>
{
    public required uint UniqueNumber { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Page1 { get; init; }
    public required int Index1 { get; init; }
    public required int Quantity1 { get; init; }
    public required int Page2 { get; init; }
    public required int Index2 { get; init; }
    public required int XPost2 { get; init; }
    public required int YPost2 { get; init; }
}
