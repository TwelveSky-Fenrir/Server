using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ChangeMaster,
    ExpectedSize = 62)]
public readonly partial record struct ChangeMasterRequest : IIncomingPacket<ChangeMasterRequest>
{
    public required int AvatarPost { get; init; }

    [FixedString(49)] public required string MasterId { get; init; }
}
