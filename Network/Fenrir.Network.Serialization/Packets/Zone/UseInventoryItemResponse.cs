using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     Value2 exists because USE_PREMIUM_LONGTIME is active in this build; without it wire size would be 17 bytes,
///     not 21.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UseInventoryItem,
    ExpectedSize = 21)]
public readonly partial record struct UseInventoryItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Value { get; init; }

    public required int Value2 { get; init; }
}
