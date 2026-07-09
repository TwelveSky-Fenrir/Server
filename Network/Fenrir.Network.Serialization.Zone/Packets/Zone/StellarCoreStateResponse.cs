using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Twin of ZC 108 minus the 8th int - 29 bytes; don't reuse ZC 108's 33-byte layout.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.StellarCoreState, ExpectedSize = 29)]
public readonly record struct StellarCoreStateResponse : IOutgoingPacket
{
    /// <summary>0=ok, 1=invalid slot, 2=inventory full.</summary>
    public required int Result { get; init; }

    public required int Sort { get; init; }

    public required int Value { get; init; }

    /// <summary>Page/PosX/PosY/ItemIndex are set for case 5 (return to inventory); -1 otherwise.</summary>
    public required int Page { get; init; }

    public required int PosX { get; init; }

    public required int PosY { get; init; }

    public required int ItemIndex { get; init; }
}
