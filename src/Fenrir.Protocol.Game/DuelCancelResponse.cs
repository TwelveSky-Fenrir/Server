using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelCancel, ExpectedSize = 1)]
public readonly partial record struct DuelCancelResponse : IOutgoingPacket
{
}
