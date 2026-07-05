using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FishingLine, ExpectedSize = 21)]
public readonly partial record struct FishingLineResponse : IOutgoingPacket
{
    /// <summary>Server-side avatar index.</summary>
    public required int ServerIndex { get; init; }

    public required uint UniqueNumber { get; init; }

    /// <summary>0=failure/no water, 1=line cast, 2=line reeled in.</summary>
    public required int Result { get; init; }

    /// <summary>0/1.</summary>
    public required int FishingState { get; init; }

    /// <summary>0..5.</summary>
    public required int FishingStep { get; init; }
}
