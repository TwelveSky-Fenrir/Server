using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DiceBattle, ExpectedSize = 23,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct DiceBattleRequest : IIncomingPacket<DiceBattleRequest>
{
    public required int DiceResult { get; init; }

    public required int DiceValue01 { get; init; }

    public required int DiceValue02 { get; init; }

    public required byte Padding0 { get; init; }

    public required byte Padding1 { get; init; }
}
