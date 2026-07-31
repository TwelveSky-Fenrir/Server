using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CostumeVisibility,
    ExpectedSize = 13, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct CostumeVisibilityRequest : IIncomingPacket<CostumeVisibilityRequest>
{
    public required int Sort { get; init; }
}
