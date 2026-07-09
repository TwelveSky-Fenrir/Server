using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Data is an opaque 100-byte buffer whose layout depends on Sort; not validated here.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAction, ExpectedSize = 113,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct TribeActionRequest : IIncomingPacket<TribeActionRequest>
{
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] Data { get; init; }
}
