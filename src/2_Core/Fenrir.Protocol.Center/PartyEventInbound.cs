using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Center;

[FenrirPacket(FenrirServer.Center, FenrirDirection.Incoming, 57, ExpectedSize = 135,
    AllowedStates = [(byte)CenterSessionState.Authenticated])]
public readonly partial record struct PartyEventInbound : IIncomingPacket<PartyEventInbound>
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
