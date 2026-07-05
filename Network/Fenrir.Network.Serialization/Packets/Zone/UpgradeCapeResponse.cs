using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     Trailing byte is real wire data (sizeof=30, not 29); modeled as an explicit Padding field since [Reserved]
///     only covers bytes before a field.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UpgradeCape, ExpectedSize = 30)]
public readonly partial record struct UpgradeCapeResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }

    /// <summary>Dead trailing byte (<c>BYTE tmp</c>), always written as 0.</summary>
    public required byte Padding { get; init; }
}
