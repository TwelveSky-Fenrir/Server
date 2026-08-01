using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SamgundoCenterInfo,
    ExpectedSize = 22)]
public readonly partial record struct SamgundoAnnouncementResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }

    public required int Tribe { get; init; }
}
