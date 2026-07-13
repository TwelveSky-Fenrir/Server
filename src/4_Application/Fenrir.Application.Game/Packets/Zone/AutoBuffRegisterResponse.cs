using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoBuffRegister,
    ExpectedSize = 5)]
public readonly partial record struct AutoBuffRegisterResponse : IOutgoingPacket
{
    public required int Value { get; init; }
}
