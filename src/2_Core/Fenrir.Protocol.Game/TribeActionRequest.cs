using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAction, ExpectedSize = 113,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeActionRequest : IIncomingPacket<TribeActionRequest>
{
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] Data { get; init; }
}
