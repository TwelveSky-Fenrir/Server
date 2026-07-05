using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Unrecognized Sort disconnects the client; Data's real layout is decoded per-Sort by the handler.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GenericAction, ExpectedSize = 143,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GenericActionRequest : IIncomingPacket<GenericActionRequest>
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
