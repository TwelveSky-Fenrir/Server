using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_IMPROVE_ITEM_SEND (CLIENT.h:257-264) — typedef SHARED with <see cref="CombineItemRequest" /> (25),
///     <see cref="UpgradeItemRankRequest" /> (27), <see cref="DowngradeItemRankRequest" /> (28): identical layout, distinct
///     C# contracts. (Page1,Index1) = target, (Page2,Index2) = material, <see cref="Luck" /> = lucky-item
///     flag. Response: ZC 27.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.EnchantItem, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct EnchantItemRequest : IIncomingPacket<EnchantItemRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Luck { get; init; }
}
