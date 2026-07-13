using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelCancel, ExpectedSize = 1)]
public readonly partial record struct DuelCancelResponse : IOutgoingPacket
{
}
