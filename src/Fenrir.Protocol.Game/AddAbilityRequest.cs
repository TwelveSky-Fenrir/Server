using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// CLIENT.h:265-272 CZ_ADD_ABILITY_SEND, typedef a 4 champs sans tLuck; mort: aucun handler ni REGWORK dans ts25zone.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AddAbility, ExpectedSize = 25,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct AddAbilityRequest : IIncomingPacket<AddAbilityRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }
}
