using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_SMELT_ITEM_SEND CLIENT.h:265-272 (typedef SANS tLuck) ; mort en M33/LNW33 : REGWORK1 sous #ifdef USE_REFINE S04_MyWork01.cpp:111-113 et #undef USE_REFINE DEFINE.h:106.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.SmeltItem, ExpectedSize = 25,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct SmeltItemRequest : IIncomingPacket<SmeltItemRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }
}
