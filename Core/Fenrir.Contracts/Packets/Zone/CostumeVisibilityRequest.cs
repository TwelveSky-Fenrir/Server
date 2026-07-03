using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_COSTUME_STATE2_SEND (CLIENT.h:311-314, typedef shared with CZ_END_PSHOP_SEND) — toggle
///     costume display (S04_MyWork02.cpp:15209). <c>Sort</c> must be strictly 0 or 1, else Quit. Stored
///     in <c>mDATA.aCostumeState</c> + <c>wState.aCostumeState</c>. Answered by ZC 177
///     (<c>ZC_COSTUME_STATE2_RECV</c>) unicast + avatar action rebroadcast (<c>B_AVATAR_ACTION_RECV</c> +
///     Broadcast11).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CostumeVisibility,
    ExpectedSize = 13, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CostumeVisibilityRequest : IIncomingPacket<CostumeVisibilityRequest>
{
    public required int Sort { get; init; }
}
