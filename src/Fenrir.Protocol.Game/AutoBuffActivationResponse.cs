using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoBuffActivation,
    ExpectedSize = 5)]
public readonly partial record struct AutoBuffActivationResponse : IOutgoingPacket
{
    public required int Value { get; init; }
}
