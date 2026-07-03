using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DuelAskSend, ExpectedSize = 26,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzDuelAskSend : IIncomingPacket<CzDuelAskSend>
{
    [FixedString(13)] public required string AvatarName { get; init; }
    public required int Sort { get; init; }
}
