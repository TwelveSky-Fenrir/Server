using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Sort: 1=view, 2=withdraw one slot (Value=slot index 0-49).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeBank, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeBankRequest : IIncomingPacket<TribeBankRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
