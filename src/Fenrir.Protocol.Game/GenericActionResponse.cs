using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GenericAction, ExpectedSize = 143)]
public readonly partial record struct GenericActionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(130)] public required byte[] Data { get; init; }
    public required int RuneValue { get; init; }
}
