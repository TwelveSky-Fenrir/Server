using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

// Dead legacy opcode: handler never reads these fields or replies; kept only so the decoder knows the frame size.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMaster,
    ExpectedSize = 62)]
public readonly record struct ChangeMasterRequest : IIncomingPacket<ChangeMasterRequest>
{
    public required int AvatarPost { get; init; }

    [FixedString(49)] public required string MasterId { get; init; }
}
