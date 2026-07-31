using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoHuntToggle, ExpectedSize = 13)]
public readonly partial record struct AutoHuntToggleResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required int AutoState { get; init; }
}
