using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Sort: 1=view, 2=withdraw one slot (Value=slot index 0-49).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeBank, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeBankRequest : IIncomingPacket<TribeBankRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
