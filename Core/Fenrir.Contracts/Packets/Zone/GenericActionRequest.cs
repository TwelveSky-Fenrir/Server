using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Unrecognized Sort disconnects the client; Data's real layout is decoded per-Sort by the handler.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GenericAction, ExpectedSize = 143,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GenericActionRequest : IIncomingPacket<GenericActionRequest>
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
