using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_DICE_BATTLE_SEND CLIENT.h:400-405 ; les 2 octets de queue viennent de S_DICE_BATTLE_SEND = sizeof + 2 CLIENT.h:681 ; mort en M33/LNW33 : opcode 96 jamais REGWORK1, hors table W_FUNCTION.
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
