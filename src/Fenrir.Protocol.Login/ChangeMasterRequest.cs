using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMaster,
    ExpectedSize = 62)]
public readonly partial record struct ChangeMasterRequest : IIncomingPacket<ChangeMasterRequest>
{
    public required int AvatarPost { get; init; }

    [FixedString(49)] public required string MasterId { get; init; }
}
