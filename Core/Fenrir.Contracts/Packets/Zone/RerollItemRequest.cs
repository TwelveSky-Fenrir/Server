using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_EXCHANGE_ITEM_SEND (CLIENT.h:232-242) — random re-roll of a Rare/Elite item (same level/tribe),
///     paid. <c>USE_EXCHANGE_ITEM_V2</c> is NOT compiled in EU33 (commented at DEFINE.h:75): no
///     <c>tTribe</c> field, 20-byte payload only. Response: ZC 29.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.RerollItem, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct RerollItemRequest : IIncomingPacket<RerollItemRequest>
{
    public required int Sort { get; init; }

    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Value1 { get; init; }

    public required int Value2 { get; init; }
}
