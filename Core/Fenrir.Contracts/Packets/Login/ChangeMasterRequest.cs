using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

// Dead legacy opcode: handler never reads these fields or replies; kept only so the decoder knows the frame size.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMaster,
    ExpectedSize = 62)]
public readonly partial record struct ChangeMasterRequest : IIncomingPacket<ChangeMasterRequest>
{
    public required int AvatarPost { get; init; }

    [FixedString(49)] public required string MasterId { get; init; }
}
