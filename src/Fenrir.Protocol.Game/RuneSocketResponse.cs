using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.RuneSocket, ExpectedSize = 21)]
public readonly partial record struct RuneSocketResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int ItemIndex { get; init; }

    public required int RuneIndex { get; init; }
}
